using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Servicios;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class ServiciosController : ControllerBase
{
    private readonly IServicioRepository _servicioRepository;
    private readonly IBitacoraRepository _bitacoraRepository;
    private readonly IPermisoService _permisoService;
    private readonly ILogger<ServiciosController> _logger;

    public ServiciosController(
        IServicioRepository servicioRepository,
        IBitacoraRepository bitacoraRepository,
        IPermisoService permisoService,
        ILogger<ServiciosController> logger)
    {
        _servicioRepository = servicioRepository ?? throw new ArgumentNullException(nameof(servicioRepository));
        _bitacoraRepository = bitacoraRepository ?? throw new ArgumentNullException(nameof(bitacoraRepository));
        _permisoService = permisoService ?? throw new ArgumentNullException(nameof(permisoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ServicioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerTodos(
        [FromQuery] string? busqueda,
        [FromQuery] byte? estado,
        [FromQuery] int pagina = 1,
        [FromQuery] int limite = 20,
        CancellationToken ct = default)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);
        if (denegado is not null) return denegado;

        if (estado is < 1 or > 5)
        {
            return BadRequest(new { mensaje = "El estado debe estar entre 1 y 5." });
        }

        return Ok(await _servicioRepository.ObtenerTodosAsync(busqueda, estado, pagina, limite, ct));
    }

    [HttpGet("resumen")]
    [ProducesResponseType(typeof(ServicioResumenDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerResumen(CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);
        if (denegado is not null) return denegado;

        return Ok(await _servicioRepository.ObtenerResumenAsync(ct));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ServicioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(int id, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);
        if (denegado is not null) return denegado;

        var servicio = await _servicioRepository.ObtenerPorIdAsync(id, ct);
        return servicio is null
            ? NotFound(new { mensaje = "El servicio solicitado no existe." })
            : Ok(servicio);
    }

    [HttpPatch("{id:int}/estado")]
    [ProducesResponseType(typeof(CambioEstadoServicioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(
        int id,
        [FromBody] CambiarServicioEstadoRequestDto dto,
        CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Editar, ct);
        if (denegado is not null) return denegado;

        if (dto.Estado is < 1 or > 5)
        {
            return BadRequest(new { mensaje = "El estado debe estar entre 1 y 5." });
        }

        var cambio = await _servicioRepository.CambiarEstadoAsync(id, dto.Estado, ct);
        if (cambio is null)
        {
            return NotFound(new { mensaje = "El servicio indicado no existe." });
        }

        await _bitacoraRepository.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = "Seguimiento de Servicios",
            Accion = "CAMBIO_ESTADO_SERVICIO",
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = User.IdUsuario(),
            Entidad = "CodigosFijos",
            IdEntidad = cambio.Servicio.IdCodigo,
            Detalle =
                $"IdCodigo={cambio.Servicio.IdCodigo}; " +
                $"CodF_SIG={cambio.Servicio.CodF_SIG ?? ""}; " +
                $"CodFijo={cambio.Servicio.CodFijo?.ToString() ?? ""}; " +
                $"Nombre={cambio.Servicio.Nombre ?? ""}; " +
                $"EstadoAnterior={cambio.EstadoAnterior}; " +
                $"EstadoNuevo={cambio.EstadoNuevo}",
            Ip = HttpContext.IpOrigen()
        }, ct);

        return Ok(cambio);
    }

    private async Task<IActionResult?> VerificarAccesoAsync(AccionPermiso accion, CancellationToken ct)
    {
        var idUsuario = User.IdUsuario();
        if (idUsuario is null) return Unauthorized();

        if (await User.PuedeAsync(_permisoService, PermisoMenu.Servicios, accion, ct))
        {
            return null;
        }

        _logger.LogWarning("Acceso denegado a Servicios: usuario {IdUsuario}, acción {Accion}.", idUsuario, accion);
        return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Acceso denegado",
            Detail = "No tiene autorización para gestionar servicios."
        });
    }
}
