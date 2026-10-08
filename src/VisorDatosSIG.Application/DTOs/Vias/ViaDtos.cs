using System.Text.Json.Serialization;

namespace VisorDatosSIG.Application.DTOs.Vias;

/// <summary>Filtros combinables y paginación de CU16.</summary>
public sealed record ViaConsultaDto
{
    public string? Nombre { get; init; }
    public string? TipoVia { get; init; }
    public string? Osmid { get; init; }
    public int Pagina { get; init; } = 1;
    public int Limite { get; init; } = 20;
}

public record ViaResumenDto
{
    public int IdVia { get; init; }
    public string? Nombre { get; init; }
    public string? TipoVia { get; init; }
    public string? Osmid { get; init; }
}

/// <summary>Detalle con geometría original en GeoJSON, coordenadas [longitud, latitud].</summary>
public sealed record ViaDetalleDto : ViaResumenDto
{
    public int? Objectid { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public object? Geometria { get; init; }
}
