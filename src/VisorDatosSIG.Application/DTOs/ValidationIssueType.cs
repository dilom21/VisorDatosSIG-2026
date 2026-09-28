namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Tipo de incidencia detectada durante la validación detallada.
/// </summary>
public enum ValidationIssueType
{
    /// <summary>El registro no tiene geometría o la geometría está vacía.</summary>
    MissingGeometry = 1,

    /// <summary>El tipo de geometría no corresponde al esperado para la capa.</summary>
    UnexpectedGeometryType = 2,

    /// <summary>La geometría es inválida según las reglas topológicas (no se repara).</summary>
    InvalidGeometry = 3,

    /// <summary>El registro es un duplicado exacto de otro registro anterior.</summary>
    ExactDuplicate = 4,

    /// <summary>Error técnico durante la lectura del registro.</summary>
    ReadError = 5,

    /// <summary>Observación de calidad de atributos (agregada, no registro por registro).</summary>
    AttributeQuality = 6
}

/// <summary>
/// Textos visibles del tipo de incidencia.
/// </summary>
public static class ValidationIssueTypeExtensions
{
    /// <summary>
    /// Devuelve el nombre del tipo de incidencia para mostrarlo al usuario.
    /// </summary>
    public static string ToDisplayName(this ValidationIssueType issueType) => issueType switch
    {
        ValidationIssueType.MissingGeometry => "Geometría ausente",
        ValidationIssueType.UnexpectedGeometryType => "Tipo de geometría inesperado",
        ValidationIssueType.InvalidGeometry => "Geometría inválida",
        ValidationIssueType.ExactDuplicate => "Duplicado exacto",
        ValidationIssueType.ReadError => "Error de lectura",
        _ => "Calidad de atributos"
    };
}
