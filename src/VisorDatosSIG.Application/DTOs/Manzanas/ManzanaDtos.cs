using System.Text.Json.Serialization;

namespace VisorDatosSIG.Application.DTOs.Manzanas;

/// <summary>
/// Filtros opcionales y paginacion de la consulta de manzanas (CU14).
/// </summary>
public sealed record ManzanaConsultaDto
{
    public string? UvMza { get; init; }
    public string? Uv { get; init; }
    public string? Mza { get; init; }
    public int Pagina { get; init; } = 1;
    public int Limite { get; init; } = 20;
}

/// <summary>
/// Proyeccion resumida de una manzana. La geometria se reserva para la consulta de detalle.
/// </summary>
public sealed record ManzanaResumenDto
{
    public int IdManzana { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? UvMza { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Uv { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Mza { get; init; }
}

/// <summary>
/// Detalle de una manzana con su geometria real preparada para una integracion futura con el visor.
/// </summary>
/// <remarks>
/// <c>Geom</c> es la unica fuente espacial. Se entrega como GeoJSON Polygon o MultiPolygon
/// conservando el orden <c>[longitud, latitud]</c>. CU14 no calcula centroide ni coordenadas
/// Longitud/Latitud independientes.
/// </remarks>
public sealed record ManzanaDetalleDto
{
    public int IdManzana { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? IdOrigen { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? UvMza { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Uv { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Mza { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public object? Geometria { get; init; }
}
