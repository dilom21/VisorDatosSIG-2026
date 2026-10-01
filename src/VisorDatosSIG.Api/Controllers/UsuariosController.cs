using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Users;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Gestión de usuarios del sistema (CU03 - Gestionar Usuarios).
/// </summary>
/// <remarks>
/// Todos los endpoints exigen token JWT y el permiso correspondiente sobre la opción de menú
/// <c>/Usuarios</c> de <c>dbo.RolMenu</c>: ver para consultar, crear para el alta y editar para las
/// modificaciones (cambio de nombre y roles, cambio de estado y restablecimiento de contraseña).
/// La autorización es dinámica, igual que en el resto de la web: el rol <c>Administrador</c>
/// conserva el acceso total implícito y ningún rol queda fijado en el atributo <c>Authorize</c>.
/// <para>
/// Traducción de resultados a HTTP: <c>404</c> cuando el usuario no existe, <c>400</c> cuando los
/// datos enviados no son válidos (login duplicado, roles inexistentes o contraseña demasiado corta)
/// y <c>204</c> cuando el restablecimiento de contraseña se aplica. Cada operación que cambia
/// estado queda auditada en <c>dbo.Bitacora</c>.
/// </para>
/// </remarks>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class UsuariosController : ControllerBase
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly IBitacoraRepository _bitacoraRepo;
    private readonly IPermisoService _permisoService;
    private readonly ILogger<UsuariosController> _logger;

    /// <summary>
    /// Inicializa el controlador con el repositorio de usuarios, la bitácora y el resolutor de
    /// permisos.
    /// </summary>
    /// <param name="usuarioRepo">Acceso a datos de usuarios y roles.</param>
    /// <param name="bitacoraRepo">Registro de auditoría de las operaciones administrativas.</param>
    /// <param name="permisoService">Resolutor de los permisos del usuario autenticado.</param>
    /// <param name="logger">Registro de diagnóstico de la API.</param>
    public UsuariosController(
        IUsuarioRepository usuarioRepo,
        IBitacoraRepository bitacoraRepo,
        IPermisoService permisoService,
        ILogger<UsuariosController> logger)
    {
        _usuarioRepo = usuarioRepo ?? throw new ArgumentNullException(nameof(usuarioRepo));
        _bitacoraRepo = bitacoraRepo ?? throw new ArgumentNullException(nameof(bitacoraRepo));
        _permisoService = permisoService ?? throw new ArgumentNullException(nameof(permisoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lista los roles disponibles para asignar a un usuario.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Roles registrados en el sistema.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el módulo de usuarios.</response>
    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerRoles(CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);

        if (denegado is not null)
        {
            return denegado;
        }

        var roles = await _usuarioRepo.ObtenerRolesAsync(ct);
        return Ok(roles);
    }

    /// <summary>
    /// Lista los usuarios registrados con sus roles y su estado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Usuarios registrados.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el módulo de usuarios.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerTodos(CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);

        if (denegado is not null)
        {
            return denegado;
        }

        var usuarios = await _usuarioRepo.ObtenerTodosAsync(ct);
        return Ok(usuarios);
    }

    /// <summary>
    /// Obtiene un usuario por su identificador.
    /// </summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Datos del usuario solicitado.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el módulo de usuarios.</response>
    /// <response code="404">El usuario indicado no existe.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(int id, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, ct);

        if (denegado is not null)
        {
            return denegado;
        }

        var usuario = await _usuarioRepo.ObtenerPorIdAsync(id, ct);
        if (usuario is null) return NotFound(new { mensaje = "Usuario no encontrado." });
        return Ok(usuario);
    }

    /// <summary>
    /// Registra un usuario nuevo con sus roles y su contraseña inicial.
    /// </summary>
    /// <param name="dto">Datos de alta del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="201">Usuario creado; devuelve el recurso en la cabecera <c>Location</c>.</response>
    /// <response code="400">Datos obligatorios incompletos, contraseña corta, roles no disponibles o login duplicado.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de alta sobre el módulo de usuarios.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Crear([FromBody] CreateUserRequestDto dto, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Crear, ct);

        if (denegado is not null)
        {
            return denegado;
        }

        if (string.IsNullOrWhiteSpace(dto.Login) || string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.PasswordInicial))
            return BadRequest(new { mensaje = "Datos obligatorios incompletos." });

        if (dto.PasswordInicial.Length < 8)
            return BadRequest(new { mensaje = "La contraseña inicial debe tener al menos 8 caracteres." });

        var rolesDisponibles = await _usuarioRepo.ObtenerRolesAsync(ct);
        if (dto.Roles is null || dto.Roles.Count == 0 || dto.Roles.Any(rol => !rolesDisponibles.Contains(rol, StringComparer.OrdinalIgnoreCase)))
            return BadRequest(new { mensaje = "Seleccione al menos un rol disponible." });

        dto = dto with { Login = dto.Login.Trim(), Nombre = dto.Nombre.Trim(), Roles = dto.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList() };
        var nuevo = await _usuarioRepo.CrearUsuarioAsync(dto, ct);
        if (nuevo is null)
            return BadRequest(new { mensaje = "El login ya se encuentra en uso." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = BitacoraEventos.ModuloAutenticacion,
            Accion = "ALTA_USUARIO",
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = User.IdUsuario(),
            Entidad = BitacoraEventos.EntidadUsuario,
            IdEntidad = nuevo.IdUsuario,
            Detalle = $"Usuario creado: {dto.Login}",
            Ip = ip
        }, ct);


        return CreatedAtAction(nameof(ObtenerPorId), new { id = nuevo.IdUsuario }, nuevo);
    }

    /// <summary>
    /// Activa o desactiva (baja lógica) un usuario.
    /// </summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="dto">Estado deseado del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Estado actualizado.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de modificación sobre el módulo de usuarios.</response>
    /// <response code="404">El usuario indicado no existe.</response>
    [HttpPatch("{id:int}/estado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(int id, [FromBody] ChangeStatusRequestDto dto, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Editar, ct);

        if (denegado is not null)
        {
            return denegado;
        }

        var exito = await _usuarioRepo.CambiarEstadoAsync(id, dto.Activo, ct);
        if (!exito) return NotFound(new { mensaje = "Usuario no encontrado." });

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = BitacoraEventos.ModuloAutenticacion,
            Accion = "CAMBIO_ESTADO",
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = User.IdUsuario(),
            Entidad = BitacoraEventos.EntidadUsuario,
            IdEntidad = id,
            Detalle = $"Estado actualizado a {dto.Activo} para ID: {id}",
            Ip = ip
        }, ct);


        return Ok(new { mensaje = "Estado actualizado." });
    }

    /// <summary>
    /// Actualiza el nombre y los roles de un usuario.
    /// </summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="dto">Datos nuevos del usuario.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="200">Usuario actualizado.</response>
    /// <response code="400">El nombre está vacío o los roles indicados no están disponibles.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de modificación sobre el módulo de usuarios.</response>
    /// <response code="404">El usuario indicado no existe.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] UpdateUserRequestDto dto, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Editar, ct);

        if (denegado is not null)
        {
            return denegado;
        }

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { mensaje = "El nombre es obligatorio." });

        var rolesDisponibles = await _usuarioRepo.ObtenerRolesAsync(ct);
        if (dto.Roles is null || dto.Roles.Count == 0 || dto.Roles.Any(rol => !rolesDisponibles.Contains(rol, StringComparer.OrdinalIgnoreCase)))
            return BadRequest(new { mensaje = "Seleccione únicamente roles disponibles." });

        var actualizado = await _usuarioRepo.ActualizarUsuarioAsync(id, dto, ct);
        if (actualizado is null)
            return NotFound(new { mensaje = "Usuario no encontrado." });

        await RegistrarCambioAsync("ACTUALIZACION_USUARIO", id, $"Nombre y roles actualizados para ID: {id}.", ct);
        return Ok(actualizado);
    }

    /// <summary>
    /// Restablece la contraseña de un usuario (CU02 aplicado por un administrador del módulo).
    /// </summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="dto">Contraseña nueva.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <response code="204">Contraseña restablecida.</response>
    /// <response code="400">La contraseña nueva no cumple la longitud mínima.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de modificación sobre el módulo de usuarios.</response>
    /// <response code="404">El usuario indicado no existe.</response>
    [HttpPost("{id:int}/restablecer-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestablecerPassword(int id, [FromBody] ResetPasswordRequestDto dto, CancellationToken ct)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Editar, ct);

        if (denegado is not null)
        {
            return denegado;
        }

        if (string.IsNullOrWhiteSpace(dto.NuevaPassword) || dto.NuevaPassword.Length < 8)
            return BadRequest(new { mensaje = "La nueva contraseña debe tener al menos 8 caracteres." });

        var actualizado = await _usuarioRepo.RestablecerPasswordAsync(id, dto.NuevaPassword, ct);
        if (!actualizado)
            return NotFound(new { mensaje = "Usuario no encontrado." });

        await RegistrarCambioAsync("RESTABLECIMIENTO_PASSWORD", id, $"Contraseña restablecida para ID: {id}.", ct);
        return NoContent();
    }

    /// <summary>
    /// Registra en <c>dbo.Bitacora</c> un cambio administrativo sobre un usuario.
    /// </summary>
    /// <param name="accion">Acción auditada.</param>
    /// <param name="idUsuarioAfectado">Identificador del usuario afectado.</param>
    /// <param name="detalle">Descripción legible del cambio.</param>
    /// <param name="ct">Token de cancelación.</param>
    private Task RegistrarCambioAsync(string accion, int idUsuarioAfectado, string detalle, CancellationToken ct)
    {
        return _bitacoraRepo.RegistrarAsync(new BitacoraRegistroDto
        {
            Modulo = BitacoraEventos.ModuloAutenticacion,
            Accion = accion,
            Resultado = BitacoraEventos.ResultadoExitoso,
            IdUsuario = User.IdUsuario(),
            Entidad = BitacoraEventos.EntidadUsuario,
            IdEntidad = idUsuarioAfectado,
            Detalle = detalle,
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString()
        }, ct);
    }

    /// <summary>
    /// Comprueba la identidad y el permiso del usuario autenticado sobre el módulo de usuarios.
    /// </summary>
    /// <param name="accion">Acción solicitada sobre el módulo.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// <c>null</c> cuando la operación está autorizada; en caso contrario, la respuesta que debe
    /// devolverse (401 sin identidad válida, 403 sin permiso).
    /// </returns>
    /// <remarks>
    /// El permiso se resuelve contra <c>dbo.RolMenu</c> en cada petición, de modo que cambiar la
    /// matriz de un rol tiene efecto inmediato. El rol <c>Administrador</c> lo conserva siempre por
    /// el acceso total implícito del resolutor de permisos.
    /// </remarks>
    private async Task<IActionResult?> VerificarAccesoAsync(AccionPermiso accion, CancellationToken ct)
    {
        var idUsuario = User.IdUsuario();

        if (idUsuario is null)
        {
            return Unauthorized();
        }

        if (!await User.PuedeAsync(_permisoService, PermisoMenu.Usuarios, accion, ct))
        {
            _logger.LogWarning(
                "Acceso denegado al módulo de usuarios: el usuario {IdUsuario} no tiene el permiso {Accion}.",
                idUsuario.Value,
                accion);

            return Problema(
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                $"No tiene permiso para {DescripcionAccion(accion)} en el módulo de usuarios.");
        }

        return null;
    }

    /// <summary>
    /// Construye una respuesta de error uniforme (RFC 7807) con el código HTTP indicado.
    /// </summary>
    /// <param name="estado">Código HTTP de la respuesta.</param>
    /// <param name="titulo">Título del problema.</param>
    /// <param name="detalle">Explicación legible; puede ser <c>null</c>.</param>
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
        _ => "operar"
    };
}
