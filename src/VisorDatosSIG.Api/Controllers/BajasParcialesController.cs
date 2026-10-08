using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.BajasParciales;
using VisorDatosSIG.Application.Exceptions;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// CU18 - Gestionar Bajas Parciales.
/// Registra y controla periodos temporales de indisponibilidad de empleados.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class BajasParcialesController : ControllerBase
{
    private readonly IBajaParcialRepository _bajaParcialRepo;
    private readonly IEmpleadoRepository _empleadoRepo;
    private readonly IBitacoraRepository _bitacoraRepo;
    private readonly IPermisoService _permisoService;
    private readonly ILogger<BajasParcialesController> _logger;

    public BajasParcialesController(
        IBajaParcialRepository bajaParcialRepo,
        IEmpleadoRepository empleadoRepo,
        IBitacoraRepository bitacoraRepo,
        IPermisoService permisoService,
        ILogger<BajasParcialesController> logger)
    {
        _bajaParcialRepo = bajaParcialRepo
            ?? throw new ArgumentNullException(nameof(bajaParcialRepo));

        _empleadoRepo = empleadoRepo
            ?? throw new ArgumentNullException(nameof(empleadoRepo));

        _bitacoraRepo = bitacoraRepo
            ?? throw new ArgumentNullException(nameof(bitacoraRepo));

        _permisoService = permisoService
            ?? throw new ArgumentNullException(nameof(permisoService));

        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Obtiene el catálogo mínimo de empleados activos para registrar una baja.
    /// </summary>
    [HttpGet("empleados")]
    [ProducesResponseType(
        typeof(IReadOnlyList<BajaParcialEmpleadoCatalogoDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerEmpleadosCatalogo(CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);

        if (denegado is not null)
        {
            return denegado;
        }

        return Ok(await _bajaParcialRepo.ObtenerEmpleadosCatalogoAsync(ct));
    }

    /// <summary>
    /// Lista bajas parciales con filtros opcionales.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(
        typeof(IEnumerable<BajaParcialResponseDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerTodos(
        [FromQuery] int? idEmpleado,
        [FromQuery] string? estado,
        CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(
            AccionPermiso.Ver,
            ct);

        if (denegado is not null)
        {
            return denegado;
        }

        await _bajaParcialRepo.SincronizarVigenciasAsync(
            DateTime.Today,
            ct);

        var bajas = await _bajaParcialRepo.ObtenerTodosAsync(
            idEmpleado,
            estado,
            ct);

        return Ok(bajas);
    }

    /// <summary>
    /// Obtiene una baja parcial por identificador.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(
        typeof(BajaParcialResponseDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(
        int id,
        CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(
            AccionPermiso.Ver,
            ct);

        if (denegado is not null)
        {
            return denegado;
        }

        await _bajaParcialRepo.SincronizarVigenciasAsync(
            DateTime.Today,
            ct);
        var baja = await _bajaParcialRepo.ObtenerPorIdAsync(id, ct);

        if (baja is null)
        {
            return NotFound(new
            {
                mensaje = "La baja parcial solicitada no existe."
            });
        }

        return Ok(baja);
    }

    /// <summary>
    /// Registra un nuevo periodo de baja parcial.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(
        typeof(BajaParcialResponseDto),
        StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Crear(
        [FromBody] CreateBajaParcialRequestDto dto,
        CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(
            AccionPermiso.Crear,
            ct);

        if (denegado is not null)
        {
            return denegado;
        }

        if (!FechasValidas(dto.FechaInicio, dto.FechaFin))
        {
            return BadRequest(new
            {
                mensaje =
                    "La fecha de fin debe ser igual o posterior a la fecha de inicio."
            });
        }

        var empleado = await _empleadoRepo.ObtenerPorIdAsync(
            dto.IdEmpleado,
            ct);

        if (empleado is null)
        {
            return NotFound(new
            {
                mensaje = "El empleado indicado no existe."
            });
        }

        if (!empleado.Activo)
        {
            return BadRequest(new
            {
                mensaje =
                    "No se puede registrar una baja parcial para un empleado inactivo."
            });
        }

        var existeSolapamiento =
            await _bajaParcialRepo.ExisteSolapamientoAsync(
                dto.IdEmpleado,
                dto.FechaInicio,
                dto.FechaFin,
                null,
                ct);

        if (existeSolapamiento)
        {
            return BadRequest(new
            {
                mensaje =
                    "El empleado ya posee una baja parcial que coincide con el periodo indicado."
            });
        }

        BajaParcialResponseDto? creada;

        try
        {
            creada = await _bajaParcialRepo.CrearAsync(dto, ct);
        }
        catch (BajaParcialSolapamientoException)
        {
            return BadRequest(new
            {
                mensaje =
                    "El empleado ya posee una baja parcial que coincide con el periodo indicado."
            });
        }

        if (creada is null)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    mensaje =
                        "No fue posible registrar la baja parcial."
                });
        }

        await _bitacoraRepo.RegistrarAsync(
            new BitacoraRegistroDto
            {
                Modulo = "Seguimiento de Servicios",
                Accion = "ALTA_BAJA_PARCIAL",
                Resultado = BitacoraEventos.ResultadoExitoso,
                IdUsuario = User.IdUsuario(),
                Entidad = "BajasParciales",
                IdEntidad = creada.IdBajaParcial,
                Detalle =
                    $"Baja parcial registrada para empleado " +
                    $"{creada.CodigoEmpleado} - {creada.NombreEmpleado}. " +
                    $"Periodo: {creada.FechaInicio:yyyy-MM-dd} " +
                    $"a {creada.FechaFin:yyyy-MM-dd}. " +
                    $"Motivo: {creada.Motivo}",
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
            },
            ct);

        return CreatedAtAction(
            nameof(ObtenerPorId),
            new { id = creada.IdBajaParcial },
            creada);
    }

    /// <summary>
    /// Modifica las fechas, motivo y observaciones de una baja parcial.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(
        typeof(BajaParcialResponseDto),
        StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(
        int id,
        [FromBody] UpdateBajaParcialRequestDto dto,
        CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(
            AccionPermiso.Editar,
            ct);

        if (denegado is not null)
        {
            return denegado;
        }

        if (!FechasValidas(dto.FechaInicio, dto.FechaFin))
        {
            return BadRequest(new
            {
                mensaje =
                    "La fecha de fin debe ser igual o posterior a la fecha de inicio."
            });
        }

        var existente = await _bajaParcialRepo.ObtenerPorIdAsync(
            id,
            ct);

        if (existente is null)
        {
            return NotFound(new
            {
                mensaje =
                    "La baja parcial que desea modificar no existe."
            });
        }

        var existeSolapamiento =
            await _bajaParcialRepo.ExisteSolapamientoAsync(
                existente.IdEmpleado,
                dto.FechaInicio,
                dto.FechaFin,
                id,
                ct);

        if (existeSolapamiento)
        {
            return BadRequest(new
            {
                mensaje =
                    "El nuevo periodo se superpone con otra baja parcial del empleado."
            });
        }

        BajaParcialResponseDto? actualizada;

        try
        {
            actualizada = await _bajaParcialRepo.ActualizarAsync(
                id,
                dto,
                ct);
        }
        catch (BajaParcialSolapamientoException)
        {
            return BadRequest(new
            {
                mensaje =
                    "El nuevo periodo se superpone con otra baja parcial del empleado."
            });
        }

        if (actualizada is null)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    mensaje =
                        "No fue posible actualizar la baja parcial."
                });
        }

        await _bitacoraRepo.RegistrarAsync(
            new BitacoraRegistroDto
            {
                Modulo = "Seguimiento de Servicios",
                Accion = "ACTUALIZACION_BAJA_PARCIAL",
                Resultado = BitacoraEventos.ResultadoExitoso,
                IdUsuario = User.IdUsuario(),
                Entidad = "BajasParciales",
                IdEntidad = id,
                Detalle =
                    $"Baja parcial actualizada para empleado " +
                    $"{actualizada.CodigoEmpleado}. " +
                    $"Periodo: {actualizada.FechaInicio:yyyy-MM-dd} " +
                    $"a {actualizada.FechaFin:yyyy-MM-dd}.",
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
            },
            ct);

        return Ok(actualizada);
    }

    /// <summary>
    /// Cambia el estado de una baja parcial:
    /// Activa, Finalizada o Cancelada.
    /// </summary>
    [HttpPatch("{id:int}/estado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(
        int id,
        [FromBody] ChangeBajaParcialEstadoDto dto,
        CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(
            AccionPermiso.Editar,
            ct);

        if (denegado is not null)
        {
            return denegado;
        }

        var existente =
            await _bajaParcialRepo.ObtenerPorIdAsync(
                id,
                ct);

        if (existente is null)
        {
            return NotFound(new
            {
                mensaje =
                    "La baja parcial indicada no existe."
            });
        }

        var estado = dto.Estado.Trim();

        if (estado.Equals(
                "Activa",
                StringComparison.OrdinalIgnoreCase))
        {
            var existeSolapamiento =
                await _bajaParcialRepo.ExisteSolapamientoAsync(
                    existente.IdEmpleado,
                    existente.FechaInicio,
                    existente.FechaFin,
                    id,
                    ct);

            if (existeSolapamiento)
            {
                return BadRequest(new
                {
                    mensaje =
                        "No se puede reactivar la baja porque existe otro periodo coincidente."
                });
            }
        }

        bool actualizado;

        try
        {
            actualizado = await _bajaParcialRepo.CambiarEstadoAsync(
                id,
                estado,
                ct);
        }
        catch (BajaParcialSolapamientoException)
        {
            return BadRequest(new
            {
                mensaje =
                    "No se puede reactivar la baja porque existe otro periodo coincidente."
            });
        }

        if (!actualizado)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    mensaje =
                        "No fue posible cambiar el estado de la baja parcial."
                });
        }

        await _bitacoraRepo.RegistrarAsync(
            new BitacoraRegistroDto
            {
                Modulo = "Seguimiento de Servicios",
                Accion = "CAMBIO_ESTADO_BAJA_PARCIAL",
                Resultado = BitacoraEventos.ResultadoExitoso,
                IdUsuario = User.IdUsuario(),
                Entidad = "BajasParciales",
                IdEntidad = id,
                Detalle =
                    $"Estado de baja parcial cambiado de " +
                    $"'{existente.Estado}' a '{estado}' para " +
                    $"{existente.CodigoEmpleado} - {existente.NombreEmpleado}.",
                Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
            },
            ct);

        return Ok(new
        {
            mensaje =
                "Estado de la baja parcial actualizado correctamente.",
            estado
        });
    }

    private async Task<IActionResult?> VerificarAccesoAsync(
        AccionPermiso accion,
        CancellationToken ct)
    {
        var idUsuario = User.IdUsuario();

        if (idUsuario is null)
        {
            return Unauthorized();
        }

        if (!await User.PuedeAsync(
                _permisoService,
                PermisoMenu.BajasParciales,
                accion,
                ct))
        {
            _logger.LogWarning(
                "Acceso denegado a Bajas Parciales: " +
                "usuario {IdUsuario}, acción {Accion}.",
                idUsuario.Value,
                accion);

            return Problema(
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                $"No tiene autorización para " +
                $"{DescripcionAccion(accion)} bajas parciales.");
        }

        return null;
    }

    private ObjectResult Problema(
        int estado,
        string titulo,
        string? detalle) =>
        StatusCode(
            estado,
            new ProblemDetails
            {
                Status = estado,
                Title = titulo,
                Detail = detalle
            });

    private static bool FechasValidas(
        DateTime fechaInicio,
        DateTime fechaFin)
    {
        return fechaInicio != default
            && fechaFin != default
            && fechaFin.Date >= fechaInicio.Date;
    }

    private static string DescripcionAccion(
        AccionPermiso accion) =>
        accion switch
        {
            AccionPermiso.Ver => "consultar",
            AccionPermiso.Crear => "registrar",
            AccionPermiso.Editar => "modificar",
            AccionPermiso.Eliminar => "eliminar",
            _ => "gestionar"
        };
}
