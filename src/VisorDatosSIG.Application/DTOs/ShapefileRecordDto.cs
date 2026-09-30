namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Registro del shapefile incluido en la previsualización.
/// </summary>
public sealed class ShapefileRecordDto
{
    /// <summary>
    /// Número de orden del registro dentro de la previsualización (base 1).
    /// No corresponde al identificador interno del shapefile.
    /// </summary>
    public required long RecordNumber { get; init; }

    /// <summary>Tipo de geometría del registro (por ejemplo: Point, Polygon, MultiPolygon).</summary>
    public required string GeometryType { get; init; }

    /// <summary>Valores de los campos del DBF para el registro.</summary>
    public required IReadOnlyDictionary<string, string?> Values { get; init; }

    /// <summary>
    /// Devuelve el valor de un campo del registro o <c>null</c> cuando el campo no está presente.
    /// </summary>
    public string? GetValue(string fieldName) =>
        Values.TryGetValue(fieldName, out var value) ? value : null;
}
