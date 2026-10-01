using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Administración de roles y permisos del sistema web (CU03 - Gestionar Roles y CU04 - Gestionar
/// Permisos).
/// </summary>
/// <remarks>
/// Todos los endpoints exigen token JWT y el permiso correspondiente sobre la opción de menú
/// <c>/Roles</c> de <c>dbo.RolMenu</c>: ver para consultar, crear para el alta y editar para las
/// modificaciones (incluida la matriz de permisos y el cambio de estado).
/// <para>
/// Traducción de resultados a HTTP: <c>404</c> cuando el rol no existe o no se administra desde la
/// web, <c>409</c> cuando el nombre está duplicado, el rol está protegido o tiene usuarios activos
/// asignados, y <c>400</c> cuando los datos enviados no son válidos. El rol del migrador y el rol
/// <c>Administrador</c> nunca se modifican desde aquí, y ningún rol se elimina: la baja es lógica.
/// </para>
/// </remarks>
[Authorize]
[ApiController]
[Route("api/roles")]
[Produces("application/json")]
public sealed class RolesController : ControllerBase
{
    /// <summary>Mensaje uniforme cuando el rol no existe o no se administra desde la web.</summary>
    private const string MensajeNoAdministrable = "El rol indicado no existe o no se administra desde la web.";

    private readonly IRolesService _rolesService;
    private readonly IPermisoService _permisoService;
    private readonly ILogger<RolesController> _logger;

    /// <summary>
    /// Inicializa el controlador con el servicio de roles y el resolutor de permisos.
    /// </summary>
    /// <param name="rolesService">Reglas de negocio de roles y permisos.</param>
    /// <param name="permisoService">Resolutor de los permisos del usuario autenticado.</param>
    /// <param name="logger">Registro de diagnóstico de la API.</param>
    public RolesController(
        IRolesService rolesService,
        IPermisoService permisoService,
        ILogger<RolesController> logger)
    {
        _rolesService = rolesService ?? throw new ArgumentNullException(nameof(rolesService));
        _permisoService = permisoService ?? throw new ArgumentNullException(nameof(permisoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lista los roles administrables desde la web (excluye el rol del migrador).
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Roles registrados con su estado y cantidad de usuarios activos.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el módulo de roles.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RolDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerRoles(CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        return Ok(await _rolesService.ObtenerRolesAsync(cancellationToken));
    }

    /// <summary>
    /// Obtiene el catálogo de permisos (árbol de opciones de menú activas) para la matriz.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Opciones activas del menú, anidadas y ordenadas.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el módulo de roles.</response>
    [HttpGet("catalogo-permisos")]
    [ProducesResponseType(typeof(IReadOnlyList<CatalogoPermisoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerCatalogoPermisos(CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        return Ok(await _rolesService.ObtenerCatalogoPermisosAsync(cancellationToken));
    }

    /// <summary>
    /// Obtiene un rol administrable por identificador.
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Rol encontrado.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el módulo de roles.</response>
    /// <response code="404">El rol no existe o no se administra desde la web.</response>
    [HttpGet("{idRol:int}")]
    [ProducesResponseType(typeof(RolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(int idRol, CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        var rol = await _rolesService.ObtenerRolAsync(idRol, cancellationToken);

        return rol is null
            ? Problema(StatusCodes.Status404NotFound, "Rol no encontrado", MensajeNoAdministrable)
            : Ok(rol);
    }

    /// <summary>
    /// Crea un rol nuevo habilitado.
    /// </summary>
    /// <remarks>
    /// El nombre debe ser único (sin distinguir mayúsculas ni acentos) y no puede coincidir con un
    /// nombre reservado por el sistema (<c>Administrador</c> o el rol del migrador).
    /// </remarks>
    /// <param name="solicitud">Nombre y descripción del rol.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="201">Rol creado.</response>
    /// <response code="400">Datos inválidos o nombre reservado.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de registro sobre el módulo de roles.</response>
    /// <response code="409">Ya existe un rol con ese nombre.</response>
    [HttpPost]
    [ProducesResponseType(typeof(RolDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear(
        [FromBody] RolSolicitudDto solicitud,
        CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Crear, cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resultado = await _rolesService.CrearRolAsync(solicitud, ContextoAuditoria(), cancellationToken);

        return resultado.IsSuccess && resultado.Rol is not null
            ? CreatedAtAction(nameof(Obtener), new { idRol = resultado.Rol.IdRol }, resultado.Rol)
            : TraducirRol(resultado);
    }

    /// <summary>
    /// Modifica el nombre y la descripción de un rol.
    /// </summary>
    /// <remarks>El rol <c>Administrador</c> está protegido y no puede renombrarse.</remarks>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="solicitud">Nombre y descripción nuevos.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Rol actualizado.</response>
    /// <response code="400">Datos inválidos o nombre reservado.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de modificación sobre el módulo de roles.</response>
    /// <response code="404">El rol no existe o no se administra desde la web.</response>
    /// <response code="409">Ya existe otro rol con ese nombre o el rol está protegido.</response>
    [HttpPatch("{idRol:int}")]
    [ProducesResponseType(typeof(RolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(
        int idRol,
        [FromBody] RolSolicitudDto solicitud,
        CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Editar, cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resultado = await _rolesService.ActualizarRolAsync(
            idRol,
            solicitud,
            ContextoAuditoria(),
            cancellationToken);

        return resultado.IsSuccess ? Ok(resultado.Rol) : TraducirRol(resultado);
    }

    /// <summary>
    /// Habilita o deshabilita lógicamente un rol.
    /// </summary>
    /// <remarks>
    /// Los roles nunca se eliminan. Un rol con usuarios activos asignados no puede deshabilitarse y
    /// el rol <c>Administrador</c> tampoco.
    /// </remarks>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="solicitud">Estado deseado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Estado actualizado (o ya vigente).</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de modificación sobre el módulo de roles.</response>
    /// <response code="404">El rol no existe o no se administra desde la web.</response>
    /// <response code="409">El rol está protegido o tiene usuarios activos asignados.</response>
    [HttpPatch("{idRol:int}/estado")]
    [ProducesResponseType(typeof(RolDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(
        int idRol,
        [FromBody] RolEstadoSolicitudDto solicitud,
        CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Editar, cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resultado = await _rolesService.CambiarEstadoAsync(
            idRol,
            solicitud.Activo,
            ContextoAuditoria(),
            cancellationToken);

        return resultado.IsSuccess ? Ok(resultado.Rol) : TraducirRol(resultado);
    }

    /// <summary>
    /// Obtiene la matriz de permisos vigente de un rol.
    /// </summary>
    /// <remarks>
    /// Devuelve una fila por opción de menú activa (agrupadores incluidos), en el mismo orden del
    /// menú lateral, para que el cliente pinte la tabla de casillas sin lógica adicional.
    /// </remarks>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Matriz de permisos del rol.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el módulo de roles.</response>
    /// <response code="404">El rol no existe o no se administra desde la web.</response>
    [HttpGet("{idRol:int}/permisos")]
    [ProducesResponseType(typeof(RolPermisosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPermisos(int idRol, CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Ver, cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        var permisos = await _rolesService.ObtenerPermisosAsync(idRol, cancellationToken);

        return permisos is null
            ? Problema(StatusCodes.Status404NotFound, "Rol no encontrado", MensajeNoAdministrable)
            : Ok(permisos);
    }

    /// <summary>
    /// Reemplaza la matriz de permisos de un rol.
    /// </summary>
    /// <remarks>
    /// El reemplazo es total e idempotente: se eliminan las filas no incluidas en la petición y se
    /// guardan las enviadas en una sola transacción junto con su evento de bitácora. Solo se
    /// almacenan las filas que otorgan al menos un permiso; el rol <c>Administrador</c> no admite
    /// cambios porque su acceso es implícito.
    /// </remarks>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="solicitud">Matriz completa de permisos.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Matriz guardada y devuelta.</response>
    /// <response code="400">La matriz incluye opciones inexistentes o inactivas.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de modificación sobre el módulo de roles.</response>
    /// <response code="404">El rol no existe o no se administra desde la web.</response>
    /// <response code="409">El rol está protegido por el sistema.</response>
    [HttpPut("{idRol:int}/permisos")]
    [ProducesResponseType(typeof(RolPermisosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GuardarPermisos(
        int idRol,
        [FromBody] RolPermisosSolicitudDto solicitud,
        CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(AccionPermiso.Editar, cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resultado = await _rolesService.GuardarPermisosAsync(
            idRol,
            solicitud,
            ContextoAuditoria(),
            cancellationToken);

        return resultado.IsSuccess ? Ok(resultado.Permisos) : TraducirPermisos(resultado);
    }

    /// <summary>
    /// Comprueba la identidad y el permiso del usuario autenticado sobre el módulo de roles.
    /// </summary>
    /// <param name="accion">Acción solicitada sobre el módulo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <c>null</c> cuando la operación está autorizada; en caso contrario, la respuesta que debe
    /// devolverse (401 sin identidad válida, 403 sin permiso).
    /// </returns>
    private async Task<IActionResult?> VerificarAccesoAsync(AccionPermiso accion, CancellationToken cancellationToken)
    {
        var idUsuario = User.IdUsuario();

        if (idUsuario is null)
        {
            return Unauthorized();
        }

        if (!await User.PuedeAsync(_permisoService, PermisoMenu.Roles, accion, cancellationToken))
        {
            _logger.LogWarning(
                "Acceso denegado al módulo de roles y permisos: el usuario {IdUsuario} no tiene el permiso {Accion}.",
                idUsuario.Value,
                accion);

            return Problema(
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                $"No tiene permiso para {DescripcionAccion(accion)} en el módulo de roles y permisos.");
        }

        return null;
    }

    /// <summary>
    /// Construye el contexto de auditoría de la operación con el usuario del token y la IP de origen.
    /// </summary>
    private ContextoAuditoriaDto ContextoAuditoria() =>
        User.ContextoAuditoria(HttpContext.IpOrigen());

    /// <summary>
    /// Traduce el resultado de una operación sobre roles al código HTTP correspondiente.
    /// </summary>
    /// <param name="resultado">Resultado devuelto por el servicio.</param>
    private IActionResult TraducirRol(RolOperacionDto resultado)
    {
        if (resultado.Estado == ResultadoRol.NoEncontrado)
        {
            return Problema(StatusCodes.Status404NotFound, "Rol no encontrado", MensajeNoAdministrable);
        }

        if (resultado.Estado is ResultadoRol.NombreReservado or ResultadoRol.DatosInvalidos)
        {
            return Problema(StatusCodes.Status400BadRequest, "Datos inválidos", resultado.Detalle);
        }

        return Problema(StatusCodes.Status409Conflict, "Operación no permitida", resultado.Detalle);
    }

    /// <summary>
    /// Traduce el resultado de guardar la matriz de permisos al código HTTP correspondiente.
    /// </summary>
    /// <param name="resultado">Resultado devuelto por el servicio.</param>
    private IActionResult TraducirPermisos(RolPermisosOperacionDto resultado)
    {
        if (resultado.Estado == ResultadoRol.NoEncontrado)
        {
            return Problema(StatusCodes.Status404NotFound, "Rol no encontrado", MensajeNoAdministrable);
        }

        if (resultado.Estado == ResultadoRol.DatosInvalidos)
        {
            return Problema(StatusCodes.Status400BadRequest, "Datos inválidos", resultado.Detalle);
        }

        return Problema(StatusCodes.Status409Conflict, "Operación no permitida", resultado.Detalle);
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
