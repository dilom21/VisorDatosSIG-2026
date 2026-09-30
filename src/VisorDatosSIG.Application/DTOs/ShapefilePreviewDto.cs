namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Previsualización de los primeros registros de un shapefile.
/// </summary>
public sealed class ShapefilePreviewDto
{
    /// <summary>Nombres de los campos del DBF que se muestran como columnas.</summary>
    public required IReadOnlyList<string> ColumnNames { get; init; }

    /// <summary>Registros leídos para la previsualización.</summary>
    public required IReadOnlyList<ShapefileRecordDto> Records { get; init; }

    /// <summary>Cantidad máxima de registros que se previsualizan.</summary>
    public required int MaxRecords { get; init; }

    /// <summary>
    /// Indica que la previsualización alcanzó el máximo permitido,
    /// por lo que el archivo contiene más registros que los mostrados.
    /// </summary>
    public bool IsLimited => Records.Count >= MaxRecords && MaxRecords > 0;

    /// <summary>
    /// Crea una previsualización vacía con la cantidad máxima de registros indicada.
    /// </summary>
    public static ShapefilePreviewDto CreateEmpty(int maxRecords) => new()
    {
        ColumnNames = Array.Empty<string>(),
        Records = Array.Empty<ShapefileRecordDto>(),
        MaxRecords = maxRecords
    };
}
