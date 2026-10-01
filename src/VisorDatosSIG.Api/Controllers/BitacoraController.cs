using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Bitacora;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Consulta de la bitácora del sistema web (CU05 - Consultar Bitácora).
/// </summary>
/// <remarks>
/// Endpoints de solo lectura, protegidos con token JWT y con el permiso de ver sobre la opción de
/// menú <c>/Bitacora</c> (<c>dbo.RolMenu</c>).
/// <para>
/// Los eventos del módulo <c>Migrador de Datos Geográficos</c> se excluyen siempre en SQL (listado,
/// catálogos y detalle): las operaciones técnicas del migrador no forman parte de la auditoría web
/// y ese comportamiento no puede desactivarse desde la petición.
/// </para>
/// </remarks>
[Authorize]
[ApiController]
[Route("api/bitacora")]
[Produces("application/json")]
public sealed class BitacoraController : ControllerBase
{
    private readonly IBitacoraConsultaService _bitacoraService;
    private readonly IPermisoService _permisoService;
    private readonly ILogger<BitacoraController> _logger;

    /// <summary>
    /// Inicializa el controlador con el servicio de consulta y el resolutor de permisos.
    /// </summary>
    /// <param name="bitacoraService">Consulta paginada de la bitácora web.</param>
    /// <param name="permisoService">Resolutor de los permisos del usuario autenticado.</param>
    /// <param name="logger">Registro de diagnóstico de la API.</param>
    public BitacoraController(
        IBitacoraConsultaService bitacoraService,
        IPermisoService permisoService,
        ILogger<BitacoraController> logger)
    {
        _bitacoraService = bitacoraService ?? throw new ArgumentNullException(nameof(bitacoraService));
        _permisoService = permisoService ?? throw new ArgumentNullException(nameof(permisoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Consulta la bitácora con filtros y paginación.
    /// </summary>
    /// <remarks>
    /// Todos los filtros son opcionales y se aplican en SQL Server: <c>buscar</c> busca el texto en
    /// módulo, acción, entidad, resultado, detalle y usuario; los demás filtros son exactos. La
    /// página se acota a 100 registros como máximo y el orden es siempre <c>FechaHora DESC</c> con
    /// desempate por <c>IdBitacora DESC</c>, de modo que la paginación es estable.
    /// </remarks>
    /// <param name="consulta">Filtros y paginación solicitados.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Página de eventos con el total de registros que cumplen los filtros.</response>
    /// <response code="400">Los filtros enviados no son válidos (por ejemplo, un rango de fechas invertido).</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el módulo de bitácora.</response>
    [HttpGet]
    [ProducesResponseType(typeof(BitacoraPaginaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Consultar(
        [FromQuery] BitacoraConsultaDto consulta,
        CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        return Ok(await _bitacoraService.ConsultarAsync(consulta, cancellationToken));
    }

    /// <summary>
    /// Devuelve los valores disponibles para los filtros del formulario.
    /// </summary>
    /// <remarks>
    /// Se obtienen de los propios eventos registrados y excluyen el módulo de migración, igual que
    /// el listado: ningún combo ofrece un valor que la consulta no pueda devolver.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Módulos, acciones, entidades, resultados y usuarios disponibles.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el módulo de bitácora.</response>
    [HttpGet("catalogos")]
    [ProducesResponseType(typeof(BitacoraCatalogosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerCatalogos(CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        return Ok(await _bitacoraService.ObtenerCatalogosAsync(cancellationToken));
    }

    /// <summary>
    /// Obtiene el detalle de un evento de la bitácora.
    /// </summary>
    /// <param name="idBitacora">Identificador del evento.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Evento encontrado.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el módulo de bitácora.</response>
    /// <response code="404">El evento no existe o pertenece al módulo de migración.</response>
    [HttpGet("{idBitacora:long}")]
    [ProducesResponseType(typeof(BitacoraItemWebDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(long idBitacora, CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        var evento = await _bitacoraService.ObtenerPorIdAsync(idBitacora, cancellationToken);

        return evento is null
            ? Problema(
                StatusCodes.Status404NotFound,
                "Evento no encontrado",
                "El evento no existe o pertenece al módulo de migración de datos geográficos.")
            : Ok(evento);
    }

    /// <summary>
    /// Comprueba la identidad y el permiso de consulta sobre el módulo de bitácora.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <c>null</c> cuando la consulta está autorizada; en caso contrario, la respuesta que debe
    /// devolverse (401 sin identidad válida, 403 sin permiso).
    /// </returns>
    private async Task<IActionResult?> VerificarAccesoAsync(CancellationToken cancellationToken)
    {
        var idUsuario = User.IdUsuario();

        if (idUsuario is null)
        {
            return Unauthorized();
        }

        if (!await User.PuedeVerAsync(_permisoService, PermisoMenu.Bitacora, cancellationToken))
        {
            _logger.LogWarning(
                "Acceso denegado al módulo de bitácora: el usuario {IdUsuario} no tiene el permiso de consulta.",
                idUsuario.Value);

            return Problema(
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                "No tiene permiso para consultar la bitácora del sistema.");
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
}
