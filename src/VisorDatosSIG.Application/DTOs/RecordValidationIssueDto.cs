namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Incidencia detectada durante la validación detallada de un shapefile.
/// </summary>
public sealed class RecordValidationIssueDto
{
    /// <summary>
    /// Posición de lectura del registro afectado (base 1).
    /// El valor 0 indica que la incidencia no corresponde a un registro concreto.
    /// No es una clave de negocio: el campo Id del DBF puede ser 0 en todos los registros.
    /// </summary>
    public required long RecordNumber { get; init; }

    /// <summary>Capa a la que pertenece la incidencia.</summary>
    public required ShapefileLayer Layer { get; init; }

    /// <summary>Nombre visible de la capa.</summary>
    public required string LayerDisplayName { get; init; }

    /// <summary>Tipo de incidencia.</summary>
    public required ValidationIssueType IssueType { get; init; }

    /// <summary>Severidad de la incidencia.</summary>
    public required ValidationSeverity Severity { get; init; }

    /// <summary>Disposición prevista del registro en una futura migración.</summary>
    public required RecordDisposition Disposition { get; init; }

    /// <summary>Mensaje comprensible de la incidencia.</summary>
    public required string Message { get; init; }

    /// <summary>Acción recomendada para la futura migración.</summary>
    public required string RecommendedAction { get; init; }

    /// <summary>Campo o elemento afectado (por ejemplo: "geometría", "UV_MZA", "CodFijo").</summary>
    public string? Field { get; init; }

    /// <summary>Valor observado cuando corresponde.</summary>
    public string? Value { get; init; }

    /// <summary>Crea una incidencia mediante la API fluida de la aplicación.</summary>
    public static RecordValidationIssueDto Create(
        long recordNumber,
        ShapefileLayer layer,
        string layerDisplayName,
        ValidationIssueType issueType,
        ValidationSeverity severity,
        RecordDisposition disposition,
        string message,
        string recommendedAction,
        string? field = null,
        string? value = null) => new()
    {
        RecordNumber = recordNumber,
        Layer = layer,
        LayerDisplayName = layerDisplayName,
        IssueType = issueType,
        Severity = severity,
        Disposition = disposition,
        Message = message,
        RecommendedAction = recommendedAction,
        Field = field,
        Value = value
    };
}
