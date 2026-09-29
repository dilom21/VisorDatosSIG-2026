using System.Diagnostics;
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

    public MigrationService(
        IShapefileReader shapefileReader,
        IShapefileValidator shapefileValidator,
        SqlMigrationWriter writer)
    {
        _shapefileReader = shapefileReader ?? throw new ArgumentNullException(nameof(shapefileReader));
        _shapefileValidator = shapefileValidator ?? throw new ArgumentNullException(nameof(shapefileValidator));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
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
            if (options.Mode == MigrationMode.Append)
            {
                return FailureResult(
                    options,
                    destinationTable,
                    "ERROR",
                    "Append todavía no está habilitado: falta aprobar una regla de duplicados contra SQL Server.",
                    stopwatch.Elapsed);
            }

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
                return FailureResult(
                    options,
                    destinationTable,
                    "ERROR",
                    "La validación del origen contiene errores y la carga fue bloqueada.",
                    stopwatch.Elapsed,
                    sourceRecords,
                    warnings,
                    errors);
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
                await SqlMigrationWriter.DeleteDestinationAsync(
                    connection,
                    transaction,
                    options.Layer,
                    cancellationToken);

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

                if (verification.DestinationRecords != insertedRecords
                    || verification.NullGeometryRecords > 0
                    || verification.WrongSridRecords > 0
                    || verification.InvalidGeometryRecords > 0)
                {
                    throw new InvalidDataException(
                        $"La verificación post-carga falló: destino={verification.DestinationRecords}, " +
                        $"esperado={insertedRecords}, geometrías NULL={verification.NullGeometryRecords}, " +
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

            return new MigrationResult
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
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new MigrationResult
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
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            errors.Add($"{exception.GetType().Name}: {exception.Message}");
            return FailureResult(
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
