namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Estado general de la inspección de un shapefile.
/// </summary>
public enum ShapefileInspectionStatus
{
    /// <summary>La inspección finalizó sin errores ni advertencias.</summary>
    Success = 1,

    /// <summary>La inspección finalizó pero requiere atención del usuario.</summary>
    Warning = 2,

    /// <summary>No fue posible inspeccionar el archivo.</summary>
    Failed = 3
}

/// <summary>
/// Textos visibles del estado de la inspección.
/// </summary>
public static class ShapefileInspectionStatusExtensions
{
    /// <summary>
    /// Devuelve el texto del estado para mostrarlo al usuario.
    /// </summary>
    public static string ToDisplayName(this ShapefileInspectionStatus status) => status switch
    {
        ShapefileInspectionStatus.Success => "Correcto",
        ShapefileInspectionStatus.Warning => "Con advertencias",
        _ => "Error"
    };
}

/// <summary>
/// Resultado completo de la inspección de un shapefile.
/// </summary>
public sealed class ShapefileInfoDto
{
    /// <summary>Ruta completa del archivo seleccionado.</summary>
    public required string FilePath { get; init; }

    /// <summary>Nombre del archivo (con extensión).</summary>
    public required string FileName { get; init; }

    /// <summary>Capa oficial detectada.</summary>
    public required ShapefileLayer Layer { get; init; }

    /// <summary>Nombre visible de la capa detectada.</summary>
    public required string LayerDisplayName { get; init; }

    /// <summary>Estado de la inspección.</summary>
    public required ShapefileInspectionStatus Status { get; init; }

    /// <summary>Mensaje descriptivo del estado.</summary>
    public required string StatusMessage { get; init; }

    /// <summary>Estado de los archivos asociados (.shp, .shx, .dbf y .prj).</summary>
    public required IReadOnlyList<ShapefileComponentDto> Components { get; init; }

    /// <summary>Campos del DBF detectados.</summary>
    public required IReadOnlyList<ShapefileFieldDto> Fields { get; init; }

    /// <summary>Previsualización de los primeros registros.</summary>
    public required ShapefilePreviewDto Preview { get; init; }

    /// <summary>Referencia espacial obtenida del archivo .prj.</summary>
    public required SpatialReferenceDto SpatialReference { get; init; }

    /// <summary>Errores que impidieron o afectaron la inspección.</summary>
    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>Advertencias detectadas durante la inspección.</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Cantidad de registros declarada por el archivo DBF.</summary>
    public int RecordCount { get; init; }

    /// <summary>Tipo de geometría declarado en el encabezado del archivo SHP.</summary>
    public string? DeclaredShapeType { get; init; }

    /// <summary>Tipos de geometría observados en los registros previsualizados.</summary>
    public IReadOnlyList<string> ObservedGeometryTypes { get; init; } = Array.Empty<string>();

    /// <summary>Extensión (bounding box) del shapefile expresada en texto.</summary>
    public string? ExtentText { get; init; }

    /// <summary>
    /// Indica si la extensión está dentro del rango válido de coordenadas geográficas
    /// (longitud -180..180, latitud -90..90).
    /// </summary>
    public bool? ExtentWithinGeographicRange { get; init; }

    /// <summary>Nombre de la codificación utilizada por el archivo DBF.</summary>
    public string? DbfEncodingName { get; init; }

    /// <summary>Indica si la inspección registró errores.</summary>
    public bool HasErrors => Errors.Count > 0;
}
