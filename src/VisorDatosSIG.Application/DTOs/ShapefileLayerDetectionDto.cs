namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Resultado de la detección de la capa oficial a partir del nombre del archivo shapefile.
/// </summary>
public sealed class ShapefileLayerDetectionDto
{
    /// <summary>Capa detectada (o <see cref="ShapefileLayer.Unrecognized"/>).</summary>
    public required ShapefileLayer Layer { get; init; }

    /// <summary>Nombre visible de la capa detectada.</summary>
    public required string LayerDisplayName { get; init; }

    /// <summary>Indica si el archivo corresponde a una capa oficial.</summary>
    public bool IsRecognized => Layer != ShapefileLayer.Unrecognized;

    /// <summary>Nombre oficial esperado del archivo para la capa detectada.</summary>
    public string? OfficialFileName { get; init; }

    /// <summary>Regla aplicada durante la detección (trazabilidad de la decisión).</summary>
    public string? AppliedRule { get; init; }

    /// <summary>
    /// Capas que también coincidieron con el nombre analizado.
    /// Cuando contiene más de un elemento la coincidencia es ambigua y se aplicó la primera regla.
    /// </summary>
    public IReadOnlyList<ShapefileLayer> MatchedLayers { get; init; } = Array.Empty<ShapefileLayer>();

    /// <summary>Indica si el nombre coincidió con más de una capa oficial.</summary>
    public bool IsAmbiguous => MatchedLayers.Count > 1;
}
