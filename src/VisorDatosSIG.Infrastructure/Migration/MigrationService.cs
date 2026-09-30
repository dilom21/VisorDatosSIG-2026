using System.Diagnostics;
using System.Text.Json;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Persistence;
using VisorDatosSIG.Infrastructure.Shapefiles;

namespace VisorDatosSIG.Infrastructure.Migration;

public sealed class MigrationService : IMigrationService
{
    private readonly IShapefileReader _shapefileReader;
    private readonly IShapefileValidator _shapefileValidator;
    private readonly SqlMigrationWriter _writer;
    private readonly IBitacoraService? _bitacoraService;

    public MigrationService(
        IShapefileReader shapefileReader,
        IShapefileValidator shapefileValidator,
        SqlMigrationWriter writer,
        IBitacoraService? bitacoraService = null)
    {
        _shapefileReader = shapefileReader ?? throw new ArgumentNullException(nameof(shapefileReader));
        _shapefileValidator = shapefileValidator ?? throw new ArgumentNullException(nameof(shapefileValidator));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        _bitacoraService = bitacoraService;
    }

    public async Task<MigrationResult> MigrateAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var stopwatch = Stopwatch.StartNew();
        var destinationTable = LayerRecordMapper.GetDestinationTable(options.Layer);
        var batchSize = options.BatchSize > 0 ? options.BatchSize : 500;
        var sourceRecords = 0;
        var processedRecords = 0;
        var insertedRecords = 0;
        var omittedRecords = 0;
        var failedRecords = 0;
        var warnings = new List<string>();
        var errors = new List<string>();

        try
        {
            Report(progress, options.Layer, "Inspeccionando origen", 0, 0, 0, 0, 0, 0, 0);
            var inspection = await _shapefileReader.InspectAsync(options.ShapefilePath, cancellationToken);
            sourceRecords = inspection.RecordCount;

            ValidateInspection(inspection, options);
            cancellationToken.ThrowIfCancellationRequested();

            Report(progress, options.Layer, "Validando registros", 0, sourceRecords, 0, 0, 0, 0, CalculateTotalBatches(sourceRecords, batchSize));
            var validation = await _shapefileValidator.ValidateAsync(
                inspection.FilePath,
                inspection.Layer,
                cancellationToken: cancellationToken);

            if (validation.Errors.Count > 0)
            {
                errors.AddRange(validation.Errors);
                var failure = FailureResult(
                    options,
                    destinationTable,
                    "ERROR",
                    "La validación del origen contiene errores y la carga fue bloqueada.",
                    stopwatch.Elapsed,
                    sourceRecords,
                    warnings,
                    errors);

                await RegistrarBitacoraAsync(failure, options.ShapefilePath, options.IdUsuario);
                return failure;
            }

            warnings.AddRange(validation.Warnings);
            var recordsToOmit = validation.Issues
                .Where(issue => issue.RecordNumber > 0 && issue.Disposition == RecordDisposition.Omit)
                .Select(issue => issue.RecordNumber)
                .ToHashSet();

            await using var connection = await _writer.OpenConnectionAsync(cancellationToken);
            await using var transaction = (Microsoft.Data.SqlClient.SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

            try
            {
                var initialRecords = 0;
                var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                if (options.Mode == MigrationMode.Replace)
                {
                    await SqlMigrationWriter.DeleteDestinationAsync(
                        connection,
                        transaction,
                        options.Layer,
                        cancellationToken);
                }
                else
                {
                    // Modo Append: consultar cantidad inicial y claves existentes para no duplicar
                    initialRecords = await SqlMigrationWriter.GetDestinationRecordCountAsync(
                        connection,
                        transaction,
                        options.Layer,
                        cancellationToken);

                    existingKeys = await SqlMigrationWriter.GetExistingNaturalKeysAsync(
                        connection,
                        transaction,
                        options.Layer,
                        cancellationToken);
                }

                var totalBatches = CalculateTotalBatches(sourceRecords, batchSize);
                var fields = Array.Empty<ShapefileFieldDto>();
                var recordNumber = 0L;

                using var reader = ShapefileAccess.OpenReader(inspection.FilePath);
                fields = ShapefileAccess.MapFields(reader.Fields).ToArray();

                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    recordNumber++;
                    processedRecords++;
                    var currentBatch = Math.Max(1, (processedRecords - 1) / batchSize + 1);

                    if (recordsToOmit.Contains(recordNumber))
                    {
                        omittedRecords++;
                        Report(progress, options.Layer, "Cargando registros", processedRecords, sourceRecords,
                            insertedRecords, omittedRecords, failedRecords, currentBatch, totalBatches);
                        continue;
                    }

                    try
                    {
                        var row = LayerRecordMapper.Map(options.Layer, reader, fields);

                        // Si es modo Append, verificar si el registro ya existe por su clave natural
                        if (options.Mode == MigrationMode.Append)
                        {
                            var naturalKeys = LayerRecordMapper.GetNaturalKeys(options.Layer, row).ToArray();
                            if (naturalKeys.Length > 0 && naturalKeys.Any(k => existingKeys.Contains(k)))
                            {
                                omittedRecords++;
                                Report(progress, options.Layer, "Cargando registros", processedRecords, sourceRecords,
                                    insertedRecords, omittedRecords, failedRecords, currentBatch, totalBatches);
                                continue;
                            }

                            foreach (var key in naturalKeys)
                            {
                                existingKeys.Add(key);
                            }
                        }

                        await SqlMigrationWriter.InsertAsync(
                            connection,
                            transaction,
                            options.Layer,
                            row,
                            cancellationToken);
                        insertedRecords++;
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        failedRecords++;
                        throw new InvalidDataException(
                            $"No se pudo migrar el registro {recordNumber:N0}: {exception.Message}",
                            exception);
                    }

                    Report(progress, options.Layer, "Cargando registros", processedRecords, sourceRecords,
                        insertedRecords, omittedRecords, failedRecords, currentBatch, totalBatches);
                }

                var verification = await SqlMigrationWriter.VerifyAsync(
                    connection,
                    transaction,
                    options.Layer,
                    cancellationToken);

                var expectedRecords = options.Mode == MigrationMode.Replace
                    ? insertedRecords
                    : (initialRecords + insertedRecords);

                if (verification.DestinationRecords != expectedRecords
                    || verification.NullGeometryRecords > 0
                    || verification.WrongSridRecords > 0
                    || verification.InvalidGeometryRecords > 0)
                {
                    throw new InvalidDataException(
                        $"La verificación post-carga falló: destino={verification.DestinationRecords}, " +
                        $"esperado={expectedRecords}, geometrías NULL={verification.NullGeometryRecords}, " +
                        $"SRID incorrecto={verification.WrongSridRecords}, " +
                        $"geometrías inválidas={verification.InvalidGeometryRecords}.");
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }

            stopwatch.Stop();
            Report(progress, options.Layer, "Migración completada", processedRecords, sourceRecords,
                insertedRecords, omittedRecords, failedRecords,
                CalculateTotalBatches(sourceRecords, batchSize),
                CalculateTotalBatches(sourceRecords, batchSize));

            var result = new MigrationResult
            {
                Layer = options.Layer,
                Mode = options.Mode,
                DestinationTable = destinationTable,
                Status = "EXITO",
                SourceRecords = sourceRecords,
                ProcessedRecords = processedRecords,
                InsertedRecords = insertedRecords,
                OmittedRecords = omittedRecords,
                FailedRecords = failedRecords,
                Duration = stopwatch.Elapsed,
                Warnings = warnings,
                Errors = errors
            };

            await RegistrarBitacoraAsync(result, options.ShapefilePath, options.IdUsuario);
            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var cancelResult = new MigrationResult
            {
                Layer = options.Layer,
                Mode = options.Mode,
                DestinationTable = destinationTable,
                Status = "CANCELADO",
                SourceRecords = sourceRecords,
                ProcessedRecords = processedRecords,
                InsertedRecords = 0,
                OmittedRecords = omittedRecords,
                FailedRecords = failedRecords,
                Duration = stopwatch.Elapsed,
                Warnings = warnings,
                Errors = ["La migración fue cancelada. La transacción fue revertida."]
            };

            await RegistrarBitacoraAsync(cancelResult, options.ShapefilePath, options.IdUsuario);
            return cancelResult;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            errors.Add($"{exception.GetType().Name}: {exception.Message}");
            var errorResult = FailureResult(
                options,
                destinationTable,
                "ERROR",
                "La migración falló y la transacción fue revertida.",
                stopwatch.Elapsed,
                sourceRecords,
                warnings,
                errors,
                processedRecords,
                insertedRecords,
                omittedRecords,
                failedRecords);

            await RegistrarBitacoraAsync(errorResult, options.ShapefilePath, options.IdUsuario);
            return errorResult;
        }
    }

    private async Task RegistrarBitacoraAsync(MigrationResult resultado, string rutaArchivo, int? idUsuario = null)
    {
        if (_bitacoraService is null)
        {
            return;
        }

        try
        {
            var detalle = JsonSerializer.Serialize(new
            {
                archivo = Path.GetFileName(rutaArchivo),
                capa = resultado.Layer.ToString(),
                modalidad = resultado.Mode.ToString(),
                origen = resultado.SourceRecords,
                procesados = resultado.ProcessedRecords,
                insertados = resultado.InsertedRecords,
                omitidos = resultado.OmittedRecords,
                fallidos = resultado.FailedRecords,
                duracionSegundos = Math.Round(resultado.Duration.TotalSeconds, 2),
                advertencias = resultado.Warnings,
                errores = resultado.Errors
            });

            await _bitacoraService.RegistrarAsync(new BitacoraEntryDto
            {
                IdUsuario = idUsuario ?? 1,
                FechaHora = DateTime.UtcNow,
                Modulo = ModulosSistema.MigradorDeDatosGeograficos,
                Accion = "Gestionar Migración",
                Entidad = resultado.DestinationTable,
                Resultado = resultado.Status,
                Detalle = detalle,
                IP = Environment.MachineName
            });
        }
        catch
        {
            // La auditoría no debe detener la aplicación
        }
    }

    private static void ValidateInspection(ShapefileInfoDto inspection, MigrationOptions options)
    {
        if (inspection.Layer != options.Layer)
        {
            throw new InvalidDataException("La capa seleccionada no coincide con la capa detectada en el archivo.");
        }

        if (inspection.Status == ShapefileInspectionStatus.Failed || inspection.HasErrors)
        {
            throw new InvalidDataException("La inspección del Shapefile contiene errores.");
        }

        var prj = inspection.Components.FirstOrDefault(component => component.Type == ShapefileComponentType.Prj);
        if (prj is null || !prj.Exists)
        {
            throw new InvalidDataException("La migración requiere el archivo .prj para verificar EPSG:4326.");
        }

        if (inspection.SpatialReference.Srid != 4326 || !inspection.SpatialReference.IsWgs84)
        {
            throw new InvalidDataException("El Shapefile no está identificado como WGS84 / EPSG:4326.");
        }
    }

    private static int CalculateTotalBatches(int totalRecords, int batchSize) =>
        totalRecords <= 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)batchSize);

    private static void Report(
        IProgress<MigrationProgress>? progress,
        ShapefileLayer layer,
        string phase,
        int processed,
        int total,
        int inserted,
        int omitted,
        int failed,
        int currentBatch,
        int totalBatches) => progress?.Report(new MigrationProgress
        {
            Layer = layer,
            Phase = phase,
            ProcessedRecords = processed,
            TotalRecords = total,
            InsertedRecords = inserted,
            OmittedRecords = omitted,
            FailedRecords = failed,
            CurrentBatch = currentBatch,
            TotalBatches = totalBatches
        });

    private static MigrationResult FailureResult(
        MigrationOptions options,
        string destinationTable,
        string status,
        string message,
        TimeSpan duration,
        int sourceRecords = 0,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? errors = null,
        int processedRecords = 0,
        int insertedRecords = 0,
        int omittedRecords = 0,
        int failedRecords = 0) => new()
        {
            Layer = options.Layer,
            Mode = options.Mode,
            DestinationTable = destinationTable,
            Status = status,
            SourceRecords = sourceRecords,
            ProcessedRecords = processedRecords,
            InsertedRecords = insertedRecords,
            OmittedRecords = omittedRecords,
            FailedRecords = failedRecords,
            Duration = duration,
            Warnings = warnings ?? Array.Empty<string>(),
            Errors = (errors ?? Array.Empty<string>()).Append(message).ToArray()
        };
}
