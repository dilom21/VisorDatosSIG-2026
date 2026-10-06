using System.Text.Json.Serialization;

namespace VisorDatosSIG.Application.DTOs.Lotes;

/// <summary>Filtros combinables y paginación de CU15.</summary>
public sealed record LoteConsultaDto
{
    public string? NroLote { get; init; }
    public int? IdManzana { get; init; }
    public int Pagina { get; init; } = 1;
    public int Limite { get; init; } = 20;
}

public record LoteResumenDto
{
    public int IdLote { get; init; }
    public string? NroLote { get; init; }
    public int? IdManzana { get; init; }
}

/// <summary>Detalle con geometría original en GeoJSON, coordenadas [longitud, latitud].</summary>
public sealed record LoteDetalleDto : LoteResumenDto
{
    public int? IdOrigen { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public object? Geometria { get; init; }
}
