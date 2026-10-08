using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.DTOs.Disponibilidad;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/disponibilidad")]
public sealed class DisponibilidadController : ControllerBase
{
    private const string Modulo = "Seguimiento de Servicios";
    private readonly IDisponibilidadRepository _repository;
    private readonly IPermisoService _permisos;
    private readonly IBitacoraService _bitacora;
    private readonly ILogger<DisponibilidadController> _logger;

    public DisponibilidadController(IDisponibilidadRepository repository, IPermisoService permisos,
        IBitacoraService bitacora, ILogger<DisponibilidadController> logger) =>
        (_repository, _permisos, _bitacora, _logger) = (repository, permisos, bitacora, logger);

    [HttpGet("empleados/{idEmpleado:int}/horarios")]
    public async Task<IActionResult> ObtenerHorarios(int idEmpleado, CancellationToken ct)
    {
        var denegado = await AccesoAsync(AccionPermiso.Ver, ct); if (denegado is not null) return denegado;
        var horarios = await _repository.ObtenerHorariosAsync(idEmpleado, ct);
        return horarios is null ? NotFound(Problema(404, "Empleado no encontrado", "El empleado indicado no existe.")) : Ok(horarios);
    }

    [HttpPost("empleados/{idEmpleado:int}/horarios")]
    public async Task<IActionResult> CrearHorario(int idEmpleado, [FromBody] GuardarHorarioRequestDto dto, CancellationToken ct)
    {
        var denegado = await AccesoAsync(AccionPermiso.Crear, ct); if (denegado is not null) return denegado;
        var resultado = await _repository.CrearHorarioAsync(idEmpleado, dto, ct);
        if (resultado.Tipo != TipoResultadoOperacion.Exito) return Convertir(resultado);
        await AuditarAsync("Crear Horario de Trabajo", "HorariosTrabajo", resultado.Valor!.IdHorario,
            $"Horario creado para empleado {idEmpleado}.", ct);
        return Created($"/api/disponibilidad/horarios/{resultado.Valor.IdHorario}", resultado.Valor);
    }

    [HttpPut("horarios/{idHorario:int}")]
    public async Task<IActionResult> ActualizarHorario(int idHorario, [FromBody] GuardarHorarioRequestDto dto, CancellationToken ct)
    {
        var denegado = await AccesoAsync(AccionPermiso.Editar, ct); if (denegado is not null) return denegado;
        var resultado = await _repository.ActualizarHorarioAsync(idHorario, dto, ct);
        if (resultado.Tipo != TipoResultadoOperacion.Exito) return Convertir(resultado);
        await AuditarAsync("Actualizar Horario de Trabajo", "HorariosTrabajo", idHorario, "Horario actualizado.", ct);
        return Ok(resultado.Valor);
    }

    [HttpPatch("horarios/{idHorario:int}/estado")]
    public async Task<IActionResult> CambiarEstadoHorario(int idHorario, [FromBody] CambiarHorarioEstadoDto dto, CancellationToken ct)
    {
        var denegado = await AccesoAsync(AccionPermiso.Editar, ct); if (denegado is not null) return denegado;
        var resultado = await _repository.CambiarEstadoHorarioAsync(idHorario, dto.Activo, ct);
        if (resultado.Tipo != TipoResultadoOperacion.Exito) return Convertir(resultado);
        await AuditarAsync("Cambiar Estado de Horario", "HorariosTrabajo", idHorario,
            $"Horario {(dto.Activo ? "activado" : "desactivado")}.", ct);
        return Ok(resultado.Valor);
    }

    [HttpGet]
    public async Task<IActionResult> Consultar([FromQuery] DateOnly? fecha, [FromQuery] TimeOnly? hora,
        [FromQuery] string? busqueda, [FromQuery] string? area, [FromQuery] string? cargo,
        [FromQuery] string? estado, CancellationToken ct)
    {
        var denegado = await AccesoAsync(AccionPermiso.Ver, ct); if (denegado is not null) return denegado;
        if (!fecha.HasValue || !hora.HasValue)
            return BadRequest(Problema(400, "Consulta inválida", "Los parámetros fecha y hora son obligatorios."));
        var fechaHora = fecha.Value.ToDateTime(hora.Value);
        var datos = await _repository.ConsultarDisponibilidadAsync(new(fechaHora, Normalizar(busqueda),
            Normalizar(area), Normalizar(cargo), Normalizar(estado)), ct);
        await AuditarAsync("Consultar Disponibilidad de Personal", "Empleados", null,
            $"Consulta para {fechaHora:yyyy-MM-dd HH:mm}; {datos.Count} empleados.", ct);
        return Ok(datos);
    }

    private async Task<IActionResult?> AccesoAsync(AccionPermiso accion, CancellationToken ct)
    {
        if (User.IdUsuario() is null) return Unauthorized();
        return await User.PuedeAsync(_permisos, PermisoMenu.DisponibilidadPersonal, accion, ct)
            ? null : StatusCode(403, Problema(403, "Acceso denegado", "No tiene permiso para gestionar disponibilidad de personal."));
    }

    private IActionResult Convertir<T>(ResultadoOperacion<T> r) => r.Tipo switch
    {
        TipoResultadoOperacion.Invalido => BadRequest(Problema(400, "Datos inválidos", r.Mensaje)),
        TipoResultadoOperacion.NoEncontrado => NotFound(Problema(404, "No encontrado", r.Mensaje)),
        TipoResultadoOperacion.Conflicto => Conflict(Problema(409, "Conflicto", r.Mensaje)),
        _ => StatusCode(500, Problema(500, "Error", "No fue posible completar la operación."))
    };

    private async Task AuditarAsync(string accion, string entidad, long? id, string detalle, CancellationToken ct)
    {
        try { await _bitacora.RegistrarAsync(new BitacoraEntryDto { IdUsuario=User.IdUsuario(), FechaHora=DateTime.UtcNow,
            Modulo=Modulo, Accion=accion, Entidad=entidad, IdEntidad=id, Resultado="EXITO", Detalle=detalle, IP=HttpContext.IpOrigen() },ct); }
        catch(Exception ex) { _logger.LogWarning(ex,"Fallo no bloqueante de bitácora CU19: {Accion}.",accion); }
    }
    private static ProblemDetails Problema(int status,string title,string? detail)=>new(){Status=status,Title=title,Detail=detail};
    private static string? Normalizar(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}
