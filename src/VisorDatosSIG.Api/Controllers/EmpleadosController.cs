using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Empleados;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Gestión de empleados operativos y seguimiento de servicios (CU04 – Gestionar Empleados).
/// </summary>
/// <remarks>
/// Permite al Administrador y al Supervisor registrar y mantener la información del personal
/// que participa en las actividades operativas del sistema (bajas parciales, disponibilidad de personal,
/// asignación de servicios y gestión de rutas).
/// </remarks>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class EmpleadosController : ControllerBase
{
    private readonly IEmpleadoRepository _empleadoRepo;
    private readonly IBitacoraRepository _bitacoraRepo;
    private readonly IPermisoService _permisoService;
    private readonly ILogger<EmpleadosController> _logger;

    public EmpleadosController(
        IEmpleadoRepository empleadoRepo,
        IBitacoraRepository bitacoraRepo,
        IPermisoService permisoService,
        ILogger<EmpleadosController> logger)
    {
        _empleadoRepo = empleadoRepo ?? throw new ArgumentNullException(nameof(empleadoRepo));
        _bitacoraRepo = bitacoraRepo ?? throw new ArgumentNullException(nameof(bitacoraRepo));
        _permisoService = permisoService ?? throw new ArgumentNullException(nameof(permisoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lista los empleados con filtros opcionales de búsqueda, cargo, disponibilidad y estado.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EmpleadoResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerTodos(
        [FromQuery] string? busqueda,
        [FromQuery] string? cargo,
        [FromQuery] string? disponibilidad,
        [FromQuery] bool? activo,
        CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);
        if (denegado is not null) return denegado;

        var empleados = await _empleadoRepo.ObtenerTodosAsync(busqueda, cargo, disponibilidad, activo, ct);
        return Ok(empleados);
    }

    /// <summary>
    /// Devuelve las métricas numéricas agregadas del personal operativo.
    /// </summary>
    [HttpGet("metricas")]
    [ProducesResponseType(typeof(EmpleadoMetricasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerMetricas(CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);
        if (denegado is not null) return denegado;

        var metricas = await _empleadoRepo.ObtenerMetricasAsync(ct);
        return Ok(metricas);
    }

    /// <summary>
    /// Obtiene los cargos disponibles en el sistema.
    /// </summary>
    [HttpGet("cargos")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerCargos(CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);
        if (denegado is not null) return denegado;

        var cargos = await _empleadoRepo.ObtenerCargosAsync(ct);
        return Ok(cargos);
    }

    /// <summary>
    /// Obtiene los estados de disponibilidad disponibles en el sistema.
    /// </summary>
    [HttpGet("disponibilidades")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerDisponibilidades(CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);
        if (denegado is not null) return denegado;

        var disponibilidades = await _empleadoRepo.ObtenerDisponibilidadesAsync(ct);
        return Ok(disponibilidades);
    }

    /// <summary>
    /// Obtiene los datos detallados de un empleado por su identificador.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmpleadoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(int id, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);
        if (denegado is not null) return denegado;

        var empleado = await _empleadoRepo.ObtenerPorIdAsync(id, ct);
        if (empleado is null)
        {
            return NotFound(new { mensaje = "El empleado solicitado no se encuentra registrado en el sistema." });
        }

        return Ok(empleado);
    }

    /// <summary>
    /// Registra un nuevo empleado operativo en el sistema.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmpleadoResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Crear([FromBody] CreateEmpleadoRequestDto dto, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Crear, ct);
        if (denegado is not null) return denegado;

        if (string.IsNullOrWhiteSpace(dto.Codigo) ||
            string.IsNullOrWhiteSpace(dto.Nombres) ||
            string.IsNullOrWhiteSpace(dto.Apellidos) ||
            string.IsNullOrWhiteSpace(dto.DocumentoIdentidad) ||
            string.IsNullOrWhiteSpace(dto.Cargo))
        {
            return BadRequest(new { mensaje = "Faltan datos obligatorios para el registro del empleado." });
        }

        if (await _empleadoRepo.ExisteCodigoAsync(dto.Codigo, null, ct))
        {
            return BadRequest(new { mensaje = $"El código de empleado '{dto.Codigo}' ya se encuentra registrado." });
        }

        if (await _empleadoRepo.ExisteDocumentoAsync(dto.DocumentoIdentidad, null, ct))
        {
            return BadRequest(new { mensaje = $"El documento de identidad '{dto.DocumentoIdentidad}' ya está asociado a otro empleado." });
        }

        var creado = await _empleadoRepo.CrearAsync(dto, ct);
        if (creado is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { mensaje = "Ocurrió un error al guardar la información del empleado." });
        }

        await _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = "Seguimiento de Servicios",
            Accion = "ALTA_EMPLEADO",
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = User.IdUsuario(),
            Entidad = "Empleados",
            IdEntidad = creado.IdEmpleado,
            Detalle = $"Empleado registrado: {creado.Codigo} - {creado.NombreCompleto} ({creado.Cargo})",
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);

        return CreatedAtAction(nameof(ObtenerPorId), new { id = creado.IdEmpleado }, creado);
    }

    /// <summary>
    /// Actualiza la información de un empleado existente.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EmpleadoResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] UpdateEmpleadoRequestDto dto, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Editar, ct);
        if (denegado is not null) return denegado;

        var existente = await _empleadoRepo.ObtenerPorIdAsync(id, ct);
        if (existente is null)
        {
            return NotFound(new { mensaje = "El empleado que desea modificar no existe o no se encuentra disponible." });
        }

        if (string.IsNullOrWhiteSpace(dto.Nombres) ||
            string.IsNullOrWhiteSpace(dto.Apellidos) ||
            string.IsNullOrWhiteSpace(dto.DocumentoIdentidad) ||
            string.IsNullOrWhiteSpace(dto.Cargo))
        {
            return BadRequest(new { mensaje = "Faltan datos obligatorios para actualizar el empleado." });
        }

        if (await _empleadoRepo.ExisteDocumentoAsync(dto.DocumentoIdentidad, id, ct))
        {
            return BadRequest(new { mensaje = $"El documento de identidad '{dto.DocumentoIdentidad}' ya está registrado con otro empleado." });
        }

        var actualizado = await _empleadoRepo.ActualizarAsync(id, dto, ct);
        if (actualizado is null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { mensaje = "No fue posible actualizar la información del empleado." });
        }

        await _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = "Seguimiento de Servicios",
            Accion = "ACTUALIZACION_EMPLEADO",
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = User.IdUsuario(),
            Entidad = "Empleados",
            IdEntidad = id,
            Detalle = $"Datos actualizados de empleado: {actualizado.Codigo} - {actualizado.NombreCompleto}",
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);

        return Ok(actualizado);
    }

    /// <summary>
    /// Cambia el estado activo/inactivo (baja lógica) de un empleado.
    /// </summary>
    [HttpPatch("{id:int}/estado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] ChangeEmpleadoEstadoDto dto, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Editar, ct);
        if (denegado is not null) return denegado;

        var existente = await _empleadoRepo.ObtenerPorIdAsync(id, ct);
        if (existente is null)
        {
            return NotFound(new { mensaje = "El empleado que desea modificar no existe en el sistema." });
        }

        var exito = await _empleadoRepo.CambiarEstadoAsync(id, dto.Activo, ct);
        if (!exito)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { mensaje = "No se pudo actualizar el estado del empleado." });
        }

        await _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = "Seguimiento de Servicios",
            Accion = dto.Activo ? "ACTIVACION_EMPLEADO" : "BAJA_LOGICA_EMPLEADO",
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = User.IdUsuario(),
            Entidad = "Empleados",
            IdEntidad = id,
            Detalle = $"Empleado {(dto.Activo ? "activado" : "desactivado (baja lógica)")}: {existente.Codigo} - {existente.NombreCompleto}",
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);

        return Ok(new { mensaje = $"Empleado {(dto.Activo ? "activado" : "desactivado")} correctamente.", activo = dto.Activo });
    }

    /// <summary>
    /// Actualiza la disponibilidad operativa del empleado (Disponible, Asignado, Baja Parcial, etc.).
    /// </summary>
    [HttpPatch("{id:int}/disponibilidad")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarDisponibilidad(int id, [FromBody] ChangeEmpleadoDisponibilidadDto dto, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Editar, ct);
        if (denegado is not null) return denegado;

        if (string.IsNullOrWhiteSpace(dto.Disponibilidad))
        {
            return BadRequest(new { mensaje = "El estado de disponibilidad es obligatorio." });
        }

        var existente = await _empleadoRepo.ObtenerPorIdAsync(id, ct);
        if (existente is null)
        {
            return NotFound(new { mensaje = "El empleado indicado no existe en el sistema." });
        }

        var exito = await _empleadoRepo.CambiarDisponibilidadAsync(id, dto.Disponibilidad, dto.Motivo, ct);
        if (!exito)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { mensaje = "No se pudo actualizar la disponibilidad del empleado." });
        }

        await _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = "Seguimiento de Servicios",
            Accion = "CAMBIO_DISPONIBILIDAD_EMPLEADO",
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = User.IdUsuario(),
            Entidad = "Empleados",
            IdEntidad = id,
            Detalle = $"Disponibilidad cambiada a '{dto.Disponibilidad}' para {existente.Codigo} - {existente.NombreCompleto}. Motivo: {dto.Motivo ?? "Sin motivo especificado"}",
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);

        return Ok(new { mensaje = "Disponibilidad actualizada correctamente.", disponibilidad = dto.Disponibilidad });
    }

    private async Task<IActionResult?> VerificarAccesoAsync(AccionPermiso accion, CancellationToken ct)
    {
        var idUsuario = User.IdUsuario();
        if (idUsuario is null)
        {
            return Unauthorized();
        }

        // El resolutor dinámico de permisos verifica contra dbo.RolMenu para /Empleados.
        // Administrador tiene acceso total implícito; Supervisor tiene acceso configurado.
        if (!await User.PuedeAsync(_permisoService, PermisoMenu.Empleados, accion, ct))
        {
            _logger.LogWarning(
                "Acceso denegado a Empleados: usuario {IdUsuario} sin permiso {Accion} sobre {Menu}.",
                idUsuario.Value,
                accion,
                PermisoMenu.Empleados);

            return Problema(
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                $"No tiene autorización para {DescripcionAccion(accion)} empleados en el sistema.");
        }

        return null;
    }

    private ObjectResult Problema(int estado, string titulo, string? detalle) =>
        StatusCode(estado, new ProblemDetails
        {
            Status = estado,
            Title = titulo,
            Detail = detalle
        });

    private static string DescripcionAccion(AccionPermiso accion) => accion switch
    {
        AccionPermiso.Ver => "consultar",
        AccionPermiso.Crear => "registrar",
        AccionPermiso.Editar => "modificar",
        AccionPermiso.Eliminar => "deshabilitar",
        _ => "gestionar"
    };
}
