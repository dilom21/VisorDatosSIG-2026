using System.Text.Json.Serialization;

namespace VisorDatosSIG.Application.Common;

/// <summary>
/// Contrato estándar RFC 7946 GeoJSON FeatureCollection para interoperabilidad con Leaflet (RT-08).
/// </summary>
public sealed record GeoJsonFeatureCollectionDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "FeatureCollection";

    [JsonPropertyName("features")]
    public IReadOnlyList<GeoJsonFeatureDto> Features { get; init; } = Array.Empty<GeoJsonFeatureDto>();
}

public sealed record GeoJsonFeatureDto
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "Feature";

    [JsonPropertyName("id")]
    public object? Id { get; init; }

    [JsonPropertyName("geometry")]
    public object? Geometry { get; init; }

    [JsonPropertyName("properties")]
    public IDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();
}
