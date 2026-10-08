using System.ComponentModel.DataAnnotations;

namespace VisorDatosSIG.Application.DTOs.Servicios;

public sealed record ServicioDto(
    int IdCodigo,
    int? CodF_SQL,
    string? CodF_SIG,
    int? CodFijo,
    string? Nombre,
    byte Estado,
    string EstadoNombre,
    DateTime FechaCambioEstado,
    int? IdLote,
    double? Longitud,
    double? Latitud);

public sealed record CambiarServicioEstadoRequestDto(
    [Range(1, 5, ErrorMessage = "El estado debe estar entre 1 y 5.")]
    byte Estado);

public sealed record CambioEstadoServicioDto(
    ServicioDto Servicio,
    byte EstadoAnterior,
    byte EstadoNuevo);

public sealed record ServicioResumenDto(
    int Total,
    int Normal,
    int ParaCorte,
    int Cortado,
    int BajaParcial,
    int BajaTotal);
