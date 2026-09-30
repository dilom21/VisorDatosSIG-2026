namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Referencia espacial del shapefile, obtenida del contenido del archivo .prj.
/// </summary>
public sealed class SpatialReferenceDto
{
    /// <summary>Indica si se pudo leer el archivo .prj.</summary>
    public required bool IsAvailable { get; init; }

    /// <summary>Código EPSG detectado, si fue posible determinarlo.</summary>
    public int? Srid { get; init; }

    /// <summary>Nombre del sistema de coordenadas declarado en el WKT.</summary>
    public string? Name { get; init; }

    /// <summary>Texto WKT original del archivo .prj.</summary>
    public string? Wkt { get; init; }

    /// <summary>Indica si el WKT describe un sistema de coordenadas geográfico.</summary>
    public bool IsGeographic { get; init; }

    /// <summary>Indica si el WKT corresponde al sistema WGS 84.</summary>
    public bool IsWgs84 { get; init; }

    /// <summary>Evidencia o justificación técnica de la detección realizada.</summary>
    public string? DetectionEvidence { get; init; }

    /// <summary>Observación cuando la referencia espacial no coincide con lo esperado.</summary>
    public string? Note { get; init; }
}
