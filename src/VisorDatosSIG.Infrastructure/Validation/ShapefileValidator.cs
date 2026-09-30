using System.Diagnostics;
using System.Globalization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.Operation.Valid;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Shapefiles;

namespace VisorDatosSIG.Infrastructure.Validation;

/// <summary>
/// Validación detallada (registro por registro) de un shapefile antes de migrarlo.
/// </summary>
/// <remarks>
/// Solo lectura: no repara geometrías, no modifica el shapefile, no elimina registros y
/// no accede a SQL Server. Una geometría inválida o con tipo inesperado no detiene el
/// análisis: el registro se marca para omitir y el recorrido continúa.
/// </remarks>
public sealed class ShapefileValidator : IShapefileValidator
{
    /// <summary>Cada cuántos registros se informa el progreso.</summary>
    public const int ProgressInterval = 200;

    /// <inheritdoc />
    public Task<ShapefileValidationResultDto> ValidateAsync(
        string shpPath,
        ShapefileLayer layer,
        IProgress<ValidationProgressDto>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shpPath))
        {
            throw new ArgumentException("La ruta del archivo shapefile no puede estar vacía.", nameof(shpPath));
        }

        // El análisis de miles de registros es intensivo en CPU y E/S: se ejecuta en un
        // hilo de trabajo para no bloquear la interfaz del Migrador.
        return Task.Run(() => Validate(shpPath, layer, progress, cancellationToken), cancellationToken);
    }

    private static ShapefileValidationResultDto Validate(
        string shpPath,
        ShapefileLayer layer,
        IProgress<ValidationProgressDto>? progress,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(shpPath);
        var fileName = Path.GetFileName(fullPath);
        var layerDisplayName = layer.ToDisplayName();

        if (layer == ShapefileLayer.Unrecognized)
        {
            return ShapefileValidationResultDto.CreateFailure(
                fullPath,
                fileName,
                layer,
                layerDisplayName,
                "No se pudo validar: capa no reconocida",
                ["El archivo no corresponde a ninguna capa oficial. Corrija el nombre del archivo y vuelva a inspeccionarlo."]);
        }

        if (!File.Exists(fullPath))
        {
            return ShapefileValidationResultDto.CreateFailure(
                fullPath,
                fileName,
                layer,
                layerDisplayName,
                "No se pudo validar: el archivo no existe",
                [$"El archivo '{fileName}' no existe en la ruta indicada."]);
        }

        var ruleSet = LayerValidationRules.CreateFor(layer);
        var duplicateRule = ruleSet.DuplicateRule;
        var duplicateDetector = duplicateRule.IsEnabled ? new ExactDuplicateDetector(duplicateRule) : null;
        var expectedGeometry = string.Join(" / ", ShapefileAccess.ExpectedGeometryNames(layer));

        var issues = new List<RecordValidationIssueDto>();
        var errors = new List<string>();
        var warnings = new List<string>();
        var recordIssues = new List<RecordValidationIssueDto>(2);
        var geometryTypeCounts = new Dictionary<string, long>(StringComparer.Ordinal);

        var analyzed = 0;
        var valid = 0;
        var withWarnings = 0;
        var omitable = 0;
        var geometryMissing = 0;
        var geometryInvalid = 0;
        var geometryValid = 0;
        var recordsWithZ = 0;

        var totalRecords = 0;
        var recordNumber = 0L;
        var readCompleted = true;
        string? declaredShapeType = null;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var reader = ShapefileAccess.OpenReader(fullPath);
            var fields = ShapefileAccess.MapFields(reader.Fields);
            var values = new Dictionary<string, string?>(fields.Count, StringComparer.OrdinalIgnoreCase);

            totalRecords = reader.RecordCount;
            declaredShapeType = ShapefileAccess.DescribeShapeType(Shapefile.GetShapeType(fullPath));

            progress?.Report(new ValidationProgressDto
            {
                Phase = "Analizando registros",
                ProcessedRecords = 0,
                TotalRecords = totalRecords
            });

            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                recordNumber++;
                analyzed++;

                ShapefileAccess.FillRecordValues(reader, fields, values);
                ruleSet.InspectRecord(values);

                var context = ruleSet.BuildRecordContext(values);
                recordIssues.Clear();

                var geometry = reader.Geometry;

                // Regla común 1: el registro debe tener geometría.
                if (geometry is null || geometry.IsEmpty)
                {
                    geometryMissing++;
                    recordIssues.Add(RecordValidationIssueDto.Create(
                        recordNumber,
                        layer,
                        layerDisplayName,
                        ValidationIssueType.MissingGeometry,
                        ValidationSeverity.Error,
                        RecordDisposition.Omit,
                        AppendContext("El registro no tiene geometría.", context),
                        "Omitir el registro en la migración y revisar el origen del dato.",
                        "geometría",
                        "sin geometría"));
                }
                else
                {
                    var geometryName = geometry.GeometryType;
                    geometryTypeCounts[geometryName] = geometryTypeCounts.GetValueOrDefault(geometryName) + 1;

                    // Regla común 2: el tipo de geometría debe ser el esperado para la capa.
                    if (!ShapefileAccess.MatchesExpectedGeometryName(layer, geometryName))
                    {
                        recordIssues.Add(RecordValidationIssueDto.Create(
                            recordNumber,
                            layer,
                            layerDisplayName,
                            ValidationIssueType.UnexpectedGeometryType,
                            ValidationSeverity.Error,
                            RecordDisposition.Omit,
                            AppendContext($"Tipo de geometría inesperado: '{geometryName}' (se esperaba {expectedGeometry}).", context),
                            "Omitir el registro y revisar el shapefile de origen.",
                            "geometría",
                            geometryName));
                    }
                    // Regla común 3: la geometría debe ser válida. No se repara ni se modifica.
                    else if (!TryValidateGeometry(geometry, out var invalidReason))
                    {
                        geometryInvalid++;
                        recordIssues.Add(RecordValidationIssueDto.Create(
                            recordNumber,
                            layer,
                            layerDisplayName,
                            ValidationIssueType.InvalidGeometry,
                            ValidationSeverity.Error,
                            RecordDisposition.Omit,
                            AppendContext($"Geometría inválida: {invalidReason}.", context),
                            "No se repara ni se modifica el archivo: omitir el registro en la migración y corregirlo en el origen.",
                            "geometría",
                            geometryName));
                    }
                    else
                    {
                        geometryValid++;
                        if (geometry.Coordinate is CoordinateZ)
                        {
                            recordsWithZ++;
                        }
                    }

                    // Regla de duplicados de la capa.
                    if (duplicateDetector is not null)
                    {
                        var keeper = duplicateDetector.Inspect(recordNumber, geometry, values);
                        if (keeper > 0 && duplicateRule.OmitRedundantRecords)
                        {
                            recordIssues.Add(RecordValidationIssueDto.Create(
                                recordNumber,
                                layer,
                                layerDisplayName,
                                ValidationIssueType.ExactDuplicate,
                                ValidationSeverity.Error,
                                RecordDisposition.Omit,
                                AppendContext(
                                    $"Duplicado exacto del registro {keeper:N0}: misma geometría y mismos atributos.",
                                    context),
                                "Omitir el registro redundante y conservar el primero.",
                                "registro",
                                $"duplicado de {keeper:N0}"));
                        }
                    }
                }

                var disposition = ClassifyRecord(recordIssues);

                if (recordIssues.Count > 0)
                {
                    issues.AddRange(recordIssues);
                }

                switch (disposition)
                {
                    case RecordDisposition.Omit:
                        omitable++;
                        break;
                    case RecordDisposition.AcceptWithWarning:
                        withWarnings++;
                        break;
                    default:
                        valid++;
                        break;
                }

                if (recordNumber % ProgressInterval == 0)
                {
                    progress?.Report(new ValidationProgressDto
                    {
                        Phase = "Analizando registros",
                        ProcessedRecords = (int)recordNumber,
                        TotalRecords = totalRecords
                    });
                }
            }

            progress?.Report(new ValidationProgressDto
            {
                Phase = "Analizando registros",
                ProcessedRecords = totalRecords > 0 ? totalRecords : (int)recordNumber,
                TotalRecords = totalRecords
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            readCompleted = false;

            var detail = ShapefileAccess.DescribeException(ex);
            errors.Add($"Error de lectura al analizar el registro {recordNumber + 1:N0}: {detail}");

            // El registro que provocó el error no forma parte del conjunto analizado:
            // se informa como incidencia omitible para que no llegue a la migración.
            issues.Add(RecordValidationIssueDto.Create(
                recordNumber + 1,
                layer,
                layerDisplayName,
                ValidationIssueType.ReadError,
                ValidationSeverity.Error,
                RecordDisposition.Omit,
                $"No fue posible leer el registro: {detail}",
                "Revisar la integridad de los archivos .shp/.dbf y repetir la validación.",
                "lectura",
                detail));
        }

        stopwatch.Stop();

        var statistics = new List<ValidationStatisticDto>
        {
            ValidationStatisticDto.FromCount("analizados", "Registros analizados", analyzed),
            ValidationStatisticDto.FromCount("geometria-presente", "Registros con geometría", analyzed - geometryMissing),
            ValidationStatisticDto.FromCount(
                "geometria-valida",
                "Geometrías válidas",
                geometryValid,
                geometryValid == analyzed ? ValidationSeverity.Info : ValidationSeverity.Warning),
            ValidationStatisticDto.FromCount(
                "geometria-invalida",
                "Geometrías inválidas",
                geometryInvalid,
                geometryInvalid > 0 ? ValidationSeverity.Error : ValidationSeverity.Info,
                "No se reparan ni se modifican: se marcan como omitibles.")
        };

        if (geometryMissing > 0)
        {
            statistics.Add(ValidationStatisticDto.FromCount(
                "geometria-ausente",
                "Registros sin geometría",
                geometryMissing,
                ValidationSeverity.Error,
                "Sin geometría el registro no puede migrarse."));
        }

        foreach (var tipo in geometryTypeCounts.OrderByDescending(par => par.Value))
        {
            statistics.Add(ValidationStatisticDto.FromCount(
                $"tipo-{tipo.Key}",
                $"Tipo de geometría: {tipo.Key}",
                tipo.Value));
        }

        if (recordsWithZ > 0)
        {
            statistics.Add(ValidationStatisticDto.FromCount(
                "coordenadas-z",
                "Registros con coordenada Z",
                recordsWithZ,
                note: "La presencia de Z o M no invalida la geometría."));
        }

        statistics.AddRange(ruleSet.BuildStatistics());

        if (duplicateDetector is not null)
        {
            if (duplicateRule.OmitRedundantRecords)
            {
                statistics.Add(ValidationStatisticDto.FromCount(
                    "duplicados-grupos",
                    "Grupos de duplicados exactos",
                    duplicateDetector.GroupCount));
                statistics.Add(ValidationStatisticDto.FromCount(
                    "duplicados-redundantes",
                    "Registros redundantes a omitir",
                    duplicateDetector.RedundantCount,
                    duplicateDetector.RedundantCount > 0 ? ValidationSeverity.Warning : ValidationSeverity.Info,
                    duplicateRule.Description));
            }
            else
            {
                statistics.Add(ValidationStatisticDto.FromCount(
                    "geometrias-duplicadas",
                    "Geometrías duplicadas exactas",
                    duplicateDetector.RedundantCount,
                    note: duplicateRule.Description));
            }

            if (duplicateDetector.FingerprintCollisions > 0)
            {
                warnings.Add($"{duplicateDetector.FingerprintCollisions:N0} registro(s) coincidieron en la huella geométrica " +
                             "pero no en la comparación exacta: no se clasificaron como duplicados.");
            }
        }

        statistics.Add(ValidationStatisticDto.FromCount("validos", "Registros válidos", valid));
        statistics.Add(ValidationStatisticDto.FromCount(
            "con-advertencias",
            "Registros con advertencias",
            withWarnings,
            withWarnings > 0 ? ValidationSeverity.Warning : ValidationSeverity.Info));
        statistics.Add(ValidationStatisticDto.FromCount(
            "omitibles",
            "Registros omitibles",
            omitable,
            omitable > 0 ? ValidationSeverity.Error : ValidationSeverity.Info,
            "No se eliminan del archivo: solo se excluyen de la futura migración."));
        statistics.Add(ValidationStatisticDto.FromCount("candidatos", "Candidatos a migración", analyzed - omitable));

        issues.AddRange(ruleSet.BuildQualityIssues());

        if (totalRecords > 0 && recordNumber != totalRecords)
        {
            warnings.Add($"El DBF declara {totalRecords:N0} registro(s) y se analizaron {recordNumber:N0}. " +
                         "Los registros marcados como eliminados en el DBF no se cuentan (misma política que la inspección).");
        }

        var summary = new LayerValidationSummaryDto
        {
            Layer = layer,
            LayerDisplayName = layerDisplayName,
            AnalyzedRecords = analyzed,
            ValidRecords = valid,
            RecordsWithWarnings = withWarnings,
            OmitableRecords = omitable,
            ErrorIssueCount = issues.Count(issue => issue.Severity == ValidationSeverity.Error),
            WarningIssueCount = issues.Count(issue => issue.Severity == ValidationSeverity.Warning),
            InfoIssueCount = issues.Count(issue => issue.Severity == ValidationSeverity.Info)
        };

        var hasAttentionIssues = issues.Any(issue => issue.Severity != ValidationSeverity.Info);

        var status = !readCompleted
            ? ShapefileInspectionStatus.Failed
            : hasAttentionIssues
                ? ShapefileInspectionStatus.Warning
                : ShapefileInspectionStatus.Success;

        var statusMessage = status switch
        {
            ShapefileInspectionStatus.Failed => "Validación incompleta por un error de lectura",
            ShapefileInspectionStatus.Warning =>
                $"Validación completada con incidencias: {summary.AnalyzedRecords:N0} registros analizados, " +
                $"{summary.ErrorIssueCount:N0} errores, {summary.WarningIssueCount:N0} advertencias, " +
                $"{summary.InfoIssueCount:N0} observaciones informativas, {summary.OmitableRecords:N0} omitibles",
            _ when issues.Count > 0 =>
                $"Validación completada: {summary.AnalyzedRecords:N0} registros analizados, " +
                $"{summary.InfoIssueCount:N0} observaciones informativas, {summary.OmitableRecords:N0} omitibles. " +
                "Sin incidencias bloqueantes",
            _ =>
                $"Validación completada sin incidencias: {summary.AnalyzedRecords:N0} registros analizados, " +
                $"{summary.OmitableRecords:N0} omitibles"
        };

        return new ShapefileValidationResultDto
        {
            FilePath = fullPath,
            FileName = fileName,
            Layer = layer,
            LayerDisplayName = layerDisplayName,
            Status = status,
            StatusMessage = statusMessage,
            Summary = summary,
            Statistics = statistics,
            Issues = issues,
            Errors = errors,
            Warnings = warnings,
            Duration = stopwatch.Elapsed,
            DeclaredShapeType = declaredShapeType
        };
    }

    /// <summary>
    /// Determina la disposición del registro a partir de sus incidencias:
    /// si alguna implica omitir, el registro se omite; si hay advertencias, se acepta con advertencia.
    /// </summary>
    private static RecordDisposition ClassifyRecord(IReadOnlyList<RecordValidationIssueDto> recordIssues)
    {
        if (recordIssues.Count == 0)
        {
            return RecordDisposition.Accept;
        }

        if (recordIssues.Any(issue => issue.Disposition == RecordDisposition.Omit))
        {
            return RecordDisposition.Omit;
        }

        return recordIssues.Any(issue => issue.Severity == ValidationSeverity.Warning)
            ? RecordDisposition.AcceptWithWarning
            : RecordDisposition.Accept;
    }

    /// <summary>
    /// Agrega el contexto del registro al mensaje de la incidencia.
    /// </summary>
    private static string AppendContext(string message, string context) =>
        string.IsNullOrEmpty(context) ? message : $"{message} ({context})";

    /// <summary>
    /// Verifica la validez topológica de la geometría sin repararla ni modificarla.
    /// </summary>
    private static bool TryValidateGeometry(Geometry geometry, out string reason)
    {
        try
        {
            var validation = new IsValidOp(geometry);
            if (validation.IsValid)
            {
                reason = string.Empty;
                return true;
            }

            var error = validation.ValidationError;
            reason = error is null ? "geometría no válida" : DescribeValidationError(error);
            return false;
        }
        catch (Exception ex)
        {
            reason = $"no se pudo verificar la validez geométrica ({ShapefileAccess.DescribeException(ex)})";
            return false;
        }
    }

    /// <summary>
    /// Describe el error topológico detectado por NetTopologySuite.
    /// </summary>
    private static string DescribeValidationError(TopologyValidationError error)
    {
        var description = TranslateTopologyError(error.ErrorType);

        return error.Coordinate is null
            ? description
            : $"{description} en la coordenada ({FormatCoordinate(error.Coordinate.X)}, {FormatCoordinate(error.Coordinate.Y)})";
    }

    private static string FormatCoordinate(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string TranslateTopologyError(TopologyValidationErrors errorType) => errorType switch
    {
        TopologyValidationErrors.RingSelfIntersection => "auto-intersección en un anillo (ring self-intersection)",
        TopologyValidationErrors.SelfIntersection => "auto-intersección (self-intersection)",
        TopologyValidationErrors.HoleOutsideShell => "un hueco está fuera del polígono contenedor",
        TopologyValidationErrors.NestedHoles => "huecos anidados",
        TopologyValidationErrors.DisconnectedInteriors => "interiores desconectados",
        TopologyValidationErrors.NestedShells => "anillos contenedores anidados",
        TopologyValidationErrors.DuplicateRings => "anillos duplicados",
        TopologyValidationErrors.TooFewPoints => "puntos insuficientes para formar el anillo",
        TopologyValidationErrors.InvalidCoordinate => "coordenada inválida (por ejemplo, NaN)",
        TopologyValidationErrors.RingNotClosed => "anillo no cerrado",
        _ => "geometría no válida"
    };
}
