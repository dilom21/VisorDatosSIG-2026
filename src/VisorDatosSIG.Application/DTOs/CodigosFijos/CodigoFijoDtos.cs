using System.Text.Json.Serialization;

namespace VisorDatosSIG.Application.DTOs.CodigosFijos;

/// <summary>
/// Filtros y paginacion de la consulta de codigos fijos (CU13).
/// </summary>
public sealed record CodigoFijoConsultaDto
{
    public string? CodFSig { get; init; }
    public int? CodFijo { get; init; }
    public string? Nombre { get; init; }
    public byte? Estado { get; init; }
    public int Pagina { get; init; } = 1;
    public int Limite { get; init; } = 20;
}

/// <summary>
/// Proyeccion resumida de un codigo fijo para resultados paginados.
/// </summary>
public sealed record CodigoFijoResumenDto
{
    public int IdCodigo { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? CodFSig { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? CodFijo { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Nombre { get; init; }
    public byte Estado { get; init; }
    public string EstadoDescripcion => CodigoFijoEstados.Describir(Estado);
}

/// <summary>
/// Informacion completa de un codigo fijo, incluida su geometria GeoJSON.
/// </summary>
public sealed record CodigoFijoDetalleDto
{
    public int IdCodigo { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? CodFSql { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? CodFSig { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? CodFijo { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? Nombre { get; init; }
    public byte Estado { get; init; }
    public string EstadoDescripcion => CodigoFijoEstados.Describir(Estado);
    public DateTime FechaCambioEstado { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public int? IdLote { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public double? Longitud { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public double? Latitud { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public object? Geometria { get; init; }
}

/// <summary>
/// Catalogo estable de estados almacenados en dbo.CodigosFijos.
/// </summary>
public static class CodigoFijoEstados
{
    public static string Describir(byte estado) => estado switch
    {
        1 => "Normal",
        2 => "Para Corte",
        3 => "Cortado",
        4 => "Baja Parcial",
        5 => "Baja Total",
        _ => "Desconocido"
    };
}
