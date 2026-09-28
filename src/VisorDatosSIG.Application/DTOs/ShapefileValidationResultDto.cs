namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Resultado completo de la validación detallada de un shapefile.
/// No implica ninguna modificación de datos: es un análisis previo a la migración.
/// </summary>
public sealed class ShapefileValidationResultDto
{
    /// <summary>Ruta completa del archivo validado.</summary>
    public required string FilePath { get; init; }

    /// <summary>Nombre del archivo validado.</summary>
    public required string FileName { get; init; }

    /// <summary>Capa validada.</summary>
    public required ShapefileLayer Layer { get; init; }

    /// <summary>Nombre visible de la capa.</summary>
    public required string LayerDisplayName { get; init; }

    /// <summary>Estado general de la validación.</summary>
    public required ShapefileInspectionStatus Status { get; init; }

    /// <summary>Mensaje descriptivo del estado.</summary>
    public required string StatusMessage { get; init; }

    /// <summary>Resumen de clasificación de los registros.</summary>
    public required LayerValidationSummaryDto Summary { get; init; }

    /// <summary>Estadísticas de calidad (no generan incidencias masivas).</summary>
    public required IReadOnlyList<ValidationStatisticDto> Statistics { get; init; }

    /// <summary>Incidencias detectadas registro por registro.</summary>
    public required IReadOnlyList<RecordValidationIssueDto> Issues { get; init; }

    /// <summary>Errores que impidieron completar la validación.</summary>
    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>Advertencias generales del proceso de validación.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Tiempo empleado en la validación.</summary>
    public TimeSpan Duration { get; init; }

    /// <summary>Tipo de geometría declarado por el archivo SHP.</summary>
    public string? DeclaredShapeType { get; init; }

    /// <summary>Indica si se registró alguna incidencia.</summary>
    public bool HasIssues => Issues.Count > 0;

    /// <summary>Indica si la validación registró errores.</summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>
    /// Crea un resultado de validación fallida (no fue posible analizar los registros).
    /// </summary>
    public static ShapefileValidationResultDto CreateFailure(
        string filePath,
        string fileName,
        ShapefileLayer layer,
        string layerDisplayName,
        string message,
        IReadOnlyList<string> errors,
        string? declaredShapeType = null) => new()
    {
        FilePath = filePath,
        FileName = fileName,
        Layer = layer,
        LayerDisplayName = layerDisplayName,
        Status = ShapefileInspectionStatus.Failed,
        StatusMessage = message,
        Summary = LayerValidationSummaryDto.CreateEmpty(layer, layerDisplayName),
        Statistics = Array.Empty<ValidationStatisticDto>(),
        Issues = Array.Empty<RecordValidationIssueDto>(),
        Errors = errors,
        Warnings = Array.Empty<string>(),
        DeclaredShapeType = declaredShapeType
    };
}
