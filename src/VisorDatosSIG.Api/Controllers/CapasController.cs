using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Controlador de servicios geoespaciales para el Visor Cartográfico (CU06 al CU11).
/// Soporta CU09 - Gestionar Elementos Geográfico, visibilidad, leyendas e identificación.
/// </summary>
/// <remarks>
/// La autorización es dinámica, igual que en el resto de la API web: exige un token JWT válido
/// (<see cref="AuthorizeAttribute"/> sin roles fijos) y el permiso de ver sobre la opción de menú
/// <c>/Visor</c> (<see cref="PermisoMenu.Visor"/>), resuelto contra <c>dbo.RolMenu</c> en cada
/// petición. Por eso:
/// <list type="bullet">
/// <item>el rol <c>Administrador</c> entra por acceso total implícito;</item>
/// <item>el rol <c>Consultor</c> entra porque <c>dbo.RolMenu</c> ya le concede <c>/Visor</c>;</item>
/// <item>un rol personalizado al que se otorgue <c>PuedeVer</c> sobre <c>/Visor</c> también entra,
/// sin recompilar ni ampliar una lista fija de roles;</item>
/// <item>un usuario que solo tenga el rol del migrador, sin permiso web, recibe 403.</item>
/// </list>
/// Listar una capa desde la interfaz no autoriza el acceso: el permiso se comprueba en cada
/// endpoint, de modo que <c>api/capas</c> no queda abierto a roles ajenos al visor.
/// </remarks>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class CapasController : ControllerBase
{
    private const int LimiteMinimo = 1;
    private const int LimiteMaximo = 10000;
    private const int LimitePorDefecto = 2000;

    private readonly ICadastreRepository _cadastreRepository;
    private readonly IBitacoraService _bitacoraService;
    private readonly IPermisoService _permisoService;
    private readonly ILogger<CapasController> _logger;

    /// <summary>
    /// Inicializa el controlador con el repositorio catastral, el servicio de bitácora y el
    /// resolutor de permisos de la aplicación web.
    /// </summary>
    /// <param name="cadastreRepository">Acceso a las geometrías y catálogos catastrales.</param>
    /// <param name="bitacoraService">Registro de auditoría de las operaciones del visor.</param>
    /// <param name="permisoService">Resolutor de los permisos del usuario autenticado.</param>
    /// <param name="logger">Registro de diagnóstico de la API.</param>
    public CapasController(
        ICadastreRepository cadastreRepository,
        IBitacoraService bitacoraService,
        IPermisoService permisoService,
        ILogger<CapasController> logger)
    {
        _cadastreRepository = cadastreRepository ?? throw new ArgumentNullException(nameof(cadastreRepository));
        _bitacoraService = bitacoraService ?? throw new ArgumentNullException(nameof(bitacoraService));
        _permisoService = permisoService ?? throw new ArgumentNullException(nameof(permisoService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Catálogo de capas geográficas disponibles en el sistema con sus conteos (CU15 - Visibilidad de Capas, CU16 - Leyendas).
    /// </summary>
    /// <remarks>Requiere el permiso de ver sobre la opción de menú <c>/Visor</c>.</remarks>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Catálogo de capas con sus conteos.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el visor.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<IDictionary<string, object?>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerCatalogoCapas(CancellationToken cancellationToken)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        var catalogo = await _cadastreRepository.GetLayersCatalogAsync(cancellationToken);
        return Ok(catalogo);
    }

    /// <summary>
    /// Obtiene las geometrías de una capa en formato GeoJSON RFC 7946 (CU09 - Gestionar Elementos Geográfico, RF-VIS-02, RF-VIS-06).
    /// Soporta Bounding Box (minX, minY, maxX, maxY) para optimización en Leaflet.
    /// El parámetro limit se acota entre 1 y 10000 para evitar sobrecarga del servidor.
    /// </summary>
    /// <remarks>Requiere el permiso de ver sobre la opción de menú <c>/Visor</c>.</remarks>
    /// <response code="200">Colección GeoJSON con las geometrías solicitadas.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el visor.</response>
    [HttpGet("{capa}/geojson")]
    [ProducesResponseType(typeof(GeoJsonFeatureCollectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ObtenerGeoJson(
        string capa,
        [FromQuery] double? minX = null,
        [FromQuery] double? minY = null,
        [FromQuery] double? maxX = null,
        [FromQuery] double? maxY = null,
        [FromQuery, Range(LimiteMinimo, LimiteMaximo)] int limit = LimitePorDefecto,
        CancellationToken cancellationToken = default)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        var limitAcotado = Math.Clamp(limit <= 0 ? LimitePorDefecto : limit, LimiteMinimo, LimiteMaximo);
        var geojson = await _cadastreRepository.GetLayerGeoJsonAsync(capa, minX, minY, maxX, maxY, limitAcotado, cancellationToken);

        // Registro de auditoría (CU09 - Gestionar Elementos Geográfico, Módulo: Visor Cartográfico)
        try
        {
            await _bitacoraService.RegistrarAsync(new BitacoraEntryDto
            {
                IdUsuario = User.IdUsuario(),
                FechaHora = DateTime.UtcNow,
                Modulo = ModulosSistema.VisorCartografico,
                Accion = "Gestionar Elementos Geográfico",
                Entidad = capa,
                Resultado = "EXITO",
                Detalle = $"Visualización de elementos geográficos de capa '{capa}'. Total elementos: {geojson.Features.Count}. Límite: {limitAcotado}.",
                IP = HttpContext.IpOrigen()
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar en bitácora la visualización de la capa {Capa}.", capa);
        }

        return Ok(geojson);
    }

    /// <summary>
    /// Identifica entidades geográficas bajo un punto de clic en el mapa (CU10 - Gestionar Entidades, RF-CON-01).
    /// </summary>
    /// <remarks>Requiere el permiso de ver sobre la opción de menú <c>/Visor</c>.</remarks>
    /// <response code="200">Entidades encontradas en el punto indicado; puede ser una lista vacía.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el visor.</response>
    [HttpGet("identificar")]
    [ProducesResponseType(typeof(IReadOnlyList<GeoJsonFeatureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Identificar(
        [FromQuery] double lng,
        [FromQuery] double lat,
        [FromQuery] double tolerancia = 10,
        CancellationToken cancellationToken = default)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        var elementos = await _cadastreRepository.IdentifyAsync(lng, lat, tolerancia, cancellationToken);

        // Registro de auditoría (CU10 - Gestionar Entidades, Módulo: Visor Cartográfico)
        try
        {
            await _bitacoraService.RegistrarAsync(new BitacoraEntryDto
            {
                IdUsuario = User.IdUsuario(),
                FechaHora = DateTime.UtcNow,
                Modulo = ModulosSistema.VisorCartografico,
                Accion = "Gestionar Entidades",
                Entidad = "Identificación",
                Resultado = "EXITO",
                Detalle = $"Identificación de entidades en ({lng:F6}, {lat:F6}) con tolerancia {tolerancia}m. Coincidencias encontradas: {elementos.Count}.",
                IP = HttpContext.IpOrigen()
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar en bitácora la identificación en coordenadas ({Lng}, {Lat}).", lng, lat);
        }

        return Ok(elementos);
    }

    /// <summary>
    /// Obtiene la extensión total (bounding box [minX, minY, maxX, maxY]) para zoom automático (CU11 - Gestionar Extensión, RF-VIS-05, RF-VIS-07).
    /// </summary>
    /// <remarks>Requiere el permiso de ver sobre la opción de menú <c>/Visor</c>.</remarks>
    /// <response code="200">Caja envolvente calculada en el orden [minX, minY, maxX, maxY].</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene permiso de consulta sobre el visor.</response>
    /// <response code="404">No existen geometrías para calcular la extensión solicitada.</response>
    [HttpGet("extension")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerExtension([FromQuery] string? capa = null, CancellationToken cancellationToken = default)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);

        if (denegado is not null)
        {
            return denegado;
        }

        var bbox = await _cadastreRepository.GetLayerExtentAsync(capa, cancellationToken);
        if (bbox is null)
        {
            return NotFound(new { mensaje = "No se encontraron geometrías para calcular la extensión." });
        }

        // Registro de auditoría (CU11 - Gestionar Extensión, Módulo: Visor Cartográfico)
        try
        {
            await _bitacoraService.RegistrarAsync(new BitacoraEntryDto
            {
                IdUsuario = User.IdUsuario(),
                FechaHora = DateTime.UtcNow,
                Modulo = ModulosSistema.VisorCartografico,
                Accion = "Gestionar Extensión",
                Entidad = string.IsNullOrWhiteSpace(capa) ? "General" : capa,
                Resultado = "EXITO",
                Detalle = string.IsNullOrWhiteSpace(capa)
                    ? $"Cálculo de extensión general: [{bbox[0]:F6}, {bbox[1]:F6}, {bbox[2]:F6}, {bbox[3]:F6}]."
                    : $"Cálculo de extensión para capa '{capa}': [{bbox[0]:F6}, {bbox[1]:F6}, {bbox[2]:F6}, {bbox[3]:F6}].",
                IP = HttpContext.IpOrigen()
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo registrar en bitácora la obtención de extensión para la capa {Capa}.", capa ?? "General");
        }

        return Ok(new
        {
            minX = bbox[0],
            minY = bbox[1],
            maxX = bbox[2],
            maxY = bbox[3],
            bbox = bbox
        });
    }

    /// <summary>
    /// Comprueba la identidad y el permiso de consulta sobre el visor cartográfico.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <c>null</c> cuando la petición está autorizada; en caso contrario, la respuesta que debe
    /// devolverse (401 sin identidad válida, 403 sin permiso de ver sobre <c>/Visor</c>).
    /// </returns>
    /// <remarks>
    /// El permiso se resuelve en cada petición contra <c>dbo.RolMenu</c> mediante
    /// <see cref="IPermisoService"/>: el administrador entra por acceso total implícito, un rol
    /// nuevo con <c>PuedeVer</c> sobre <c>/Visor</c> entra sin tocar el código y un usuario sin
    /// permiso web (por ejemplo, el del migrador) recibe 403.
    /// </remarks>
    private async Task<IActionResult?> VerificarAccesoAsync(CancellationToken cancellationToken)
    {
        var idUsuario = User.IdUsuario();

        if (idUsuario is null)
        {
            return Unauthorized();
        }

        if (!await User.PuedeVerAsync(_permisoService, PermisoMenu.Visor, cancellationToken))
        {
            _logger.LogWarning(
                "Acceso denegado al visor cartográfico: el usuario {IdUsuario} no tiene el permiso de consulta sobre {Menu}.",
                idUsuario.Value,
                PermisoMenu.Visor);

            return Problema(
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                "No tiene permiso para consultar el visor cartográfico.");
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
