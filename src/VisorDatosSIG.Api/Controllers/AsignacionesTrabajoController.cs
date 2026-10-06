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
[Route("api/asignaciones-trabajo")]
public sealed class AsignacionesTrabajoController : ControllerBase
{
    private readonly IDisponibilidadRepository _repository; private readonly IPermisoService _permisos;
    private readonly IBitacoraService _bitacora; private readonly ILogger<AsignacionesTrabajoController> _logger;
    public AsignacionesTrabajoController(IDisponibilidadRepository repository,IPermisoService permisos,
        IBitacoraService bitacora,ILogger<AsignacionesTrabajoController> logger)=>(_repository,_permisos,_bitacora,_logger)=(repository,permisos,bitacora,logger);

    [HttpGet]
    public async Task<IActionResult> Obtener([FromQuery]int? idEmpleado,[FromQuery]int? idCodigo,[FromQuery]string? estado,
        [FromQuery]string? tipoTarea,[FromQuery]DateOnly? fecha,[FromQuery]int pagina=1,[FromQuery]int limite=20,CancellationToken ct=default)
    {
        var d=await AccesoAsync(AccionPermiso.Ver,ct);if(d is not null)return d;
        if(pagina<1||limite is <1 or >100)return BadRequest(Problema(400,"Consulta inválida","pagina debe ser >= 1 y limite entre 1 y 100."));
        return Ok(await _repository.ObtenerAsignacionesAsync(new(){IdEmpleado=idEmpleado,IdCodigo=idCodigo,Estado=Norm(estado),TipoTarea=Norm(tipoTarea),Fecha=fecha,Pagina=pagina,Limite=limite},ct));
    }
    [HttpGet("{idAsignacion:int}")]
    public async Task<IActionResult> ObtenerPorId(int idAsignacion,CancellationToken ct){var d=await AccesoAsync(AccionPermiso.Ver,ct);if(d is not null)return d;var x=await _repository.ObtenerAsignacionAsync(idAsignacion,ct);return x is null?NotFound(Problema(404,"No encontrado","La asignación no existe.")):Ok(x);}
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody]GuardarAsignacionTrabajoDto dto,CancellationToken ct){var d=await AccesoAsync(AccionPermiso.Crear,ct);if(d is not null)return d;var r=await _repository.CrearAsignacionAsync(dto,ct);if(r.Tipo!=TipoResultadoOperacion.Exito)return Convertir(r);await Auditar("Crear Asignación de Trabajo",r.Valor!.IdAsignacion,"Asignación creada.",ct);return CreatedAtAction(nameof(ObtenerPorId),new{idAsignacion=r.Valor.IdAsignacion},r.Valor);}
    [HttpPut("{idAsignacion:int}")]
    public async Task<IActionResult> Actualizar(int idAsignacion,[FromBody]GuardarAsignacionTrabajoDto dto,CancellationToken ct){var d=await AccesoAsync(AccionPermiso.Editar,ct);if(d is not null)return d;var r=await _repository.ActualizarAsignacionAsync(idAsignacion,dto,ct);if(r.Tipo!=TipoResultadoOperacion.Exito)return Convertir(r);await Auditar("Actualizar Asignación de Trabajo",idAsignacion,"Asignación actualizada.",ct);return Ok(r.Valor);}
    [HttpPatch("{idAsignacion:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int idAsignacion,[FromBody]CambiarAsignacionEstadoDto dto,CancellationToken ct){var d=await AccesoAsync(AccionPermiso.Editar,ct);if(d is not null)return d;var r=await _repository.CambiarEstadoAsignacionAsync(idAsignacion,dto.Estado,ct);if(r.Tipo!=TipoResultadoOperacion.Exito)return Convertir(r);await Auditar("Cambiar Estado de Asignación",idAsignacion,$"Estado cambiado a {dto.Estado}.",ct);return Ok(r.Valor);}

    private async Task<IActionResult?> AccesoAsync(AccionPermiso accion,CancellationToken ct){if(User.IdUsuario() is null)return Unauthorized();return await User.PuedeAsync(_permisos,PermisoMenu.DisponibilidadPersonal,accion,ct)?null:StatusCode(403,Problema(403,"Acceso denegado","No tiene permiso para gestionar asignaciones de trabajo."));}
    private IActionResult Convertir<T>(ResultadoOperacion<T> r)=>r.Tipo switch{TipoResultadoOperacion.Invalido=>BadRequest(Problema(400,"Datos inválidos",r.Mensaje)),TipoResultadoOperacion.NoEncontrado=>NotFound(Problema(404,"No encontrado",r.Mensaje)),TipoResultadoOperacion.Conflicto=>Conflict(Problema(409,"Conflicto",r.Mensaje)),_=>StatusCode(500)};
    private async Task Auditar(string accion,long id,string detalle,CancellationToken ct){try{await _bitacora.RegistrarAsync(new BitacoraEntryDto{IdUsuario=User.IdUsuario(),FechaHora=DateTime.UtcNow,Modulo="Seguimiento de Servicios",Accion=accion,Entidad="AsignacionesTrabajo",IdEntidad=id,Resultado="EXITO",Detalle=detalle,IP=HttpContext.IpOrigen()},ct);}catch(Exception ex){_logger.LogWarning(ex,"Fallo no bloqueante de bitácora CU19: {Accion}.",accion);}}
    private static ProblemDetails Problema(int status,string title,string? detail)=>new(){Status=status,Title=title,Detail=detail}; private static string? Norm(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
}
