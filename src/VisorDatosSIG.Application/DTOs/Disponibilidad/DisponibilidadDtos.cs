using System.ComponentModel.DataAnnotations;
using VisorDatosSIG.Application.Common;

namespace VisorDatosSIG.Application.DTOs.Disponibilidad;

public sealed record HorarioTrabajoDto(
    int IdHorario,
    int IdEmpleado,
    byte DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    bool Activo,
    DateTime FechaRegistro,
    DateTime? FechaModificacion);

public sealed record GuardarHorarioRequestDto(
    [Range(1, 7)] byte DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFin);

public sealed record CambiarHorarioEstadoDto(bool Activo);

public sealed record AsignacionActualDto(
    int IdAsignacion,
    string TipoTarea,
    DateTime FechaInicio,
    DateTime FechaFin,
    string Estado,
    string Prioridad);

public sealed record DisponibilidadEmpleadoDto(
    int IdEmpleado,
    string Codigo,
    string NombreCompleto,
    string Cargo,
    string Area,
    string EstadoDisponibilidad,
    string Motivo,
    string? HorarioAplicable,
    AsignacionActualDto? AsignacionActual);

public sealed record DisponibilidadConsultaDto(
    DateTime FechaHora,
    string? Busqueda = null,
    string? Area = null,
    string? Cargo = null,
    string? Estado = null);

public sealed record AsignacionTrabajoConsultaDto
{
    public int? IdEmpleado { get; init; }
    public int? IdCodigo { get; init; }
    public string? Estado { get; init; }
    public string? TipoTarea { get; init; }
    public DateOnly? Fecha { get; init; }
    public int Pagina { get; init; } = 1;
    public int Limite { get; init; } = 20;
}

public sealed record GuardarAsignacionTrabajoDto(
    [Range(1, int.MaxValue)] int IdEmpleado,
    int? IdCodigo,
    [Required, StringLength(30)] string TipoTarea,
    DateTime FechaInicio,
    DateTime FechaFin,
    [Required, StringLength(20)] string Estado,
    [Required, StringLength(15)] string Prioridad,
    [StringLength(500)] string? Observaciones);

public sealed record CambiarAsignacionEstadoDto(
    [Required, StringLength(20)] string Estado);

public sealed record AsignacionTrabajoResumenDto(
    int IdAsignacion,
    int IdEmpleado,
    string CodigoEmpleado,
    string NombreEmpleado,
    int? IdCodigo,
    string? CodigoFijo,
    string TipoTarea,
    DateTime FechaInicio,
    DateTime FechaFin,
    string Estado,
    string Prioridad);

public sealed record AsignacionTrabajoDetalleDto(
    int IdAsignacion,
    int IdEmpleado,
    string CodigoEmpleado,
    string NombreEmpleado,
    int? IdCodigo,
    string? CodigoFijo,
    string? NombreCodigoFijo,
    double? Longitud,
    double? Latitud,
    string TipoTarea,
    DateTime FechaInicio,
    DateTime FechaFin,
    string Estado,
    string Prioridad,
    string? Observaciones,
    DateTime FechaRegistro,
    DateTime? FechaModificacion);

public enum TipoResultadoOperacion
{
    Exito,
    Invalido,
    NoEncontrado,
    Conflicto
}

public sealed record ResultadoOperacion<T>(TipoResultadoOperacion Tipo, T? Valor, string? Mensaje)
{
    public static ResultadoOperacion<T> Exito(T valor) => new(TipoResultadoOperacion.Exito, valor, null);
    public static ResultadoOperacion<T> Invalido(string mensaje) => new(TipoResultadoOperacion.Invalido, default, mensaje);
    public static ResultadoOperacion<T> NoEncontrado(string mensaje) => new(TipoResultadoOperacion.NoEncontrado, default, mensaje);
    public static ResultadoOperacion<T> Conflicto(string mensaje) => new(TipoResultadoOperacion.Conflicto, default, mensaje);
}

public static class CatalogosDisponibilidad
{
    public static readonly IReadOnlySet<string> TiposTarea = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Corte", "Reconexión", "Cobro", "Inspección", "Lectura", "Otro" };
    public static readonly IReadOnlySet<string> EstadosAsignacion = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Asignada", "En Proceso", "Finalizada", "Cancelada" };
    public static readonly IReadOnlySet<string> Prioridades = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Baja", "Normal", "Alta", "Urgente" };
    public static bool Ocupa(string? estado) =>
        estado is not null && (estado.Equals("Asignada", StringComparison.OrdinalIgnoreCase)
            || estado.Equals("En Proceso", StringComparison.OrdinalIgnoreCase));
}
