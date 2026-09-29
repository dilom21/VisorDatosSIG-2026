using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class CapasController : ControllerBase
{
    private readonly ICadastreRepository _cadastreRepository;
    private readonly ILogger<CapasController> _logger;

    public CapasController(ICadastreRepository cadastreRepository, ILogger<CapasController> logger)
    {
        _cadastreRepository = cadastreRepository ?? throw new ArgumentNullException(nameof(cadastreRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Catálogo de capas geográficas disponibles en el sistema con sus conteos (CU15 - Visibilidad de Capas, CU16 - Leyendas).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<IDictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerCatalogoCapas(CancellationToken cancellationToken)
    {
        var catalogo = await _cadastreRepository.GetLayersCatalogAsync(cancellationToken);
        return Ok(catalogo);
    }

    /// <summary>
    /// Obtiene las geometrías de una capa en formato GeoJSON RFC 7946 (CU14 - Mapa, CU17 - Elementos Geográficos).
    /// Soporta Bounding Box (minX, minY, maxX, maxY) para optimización en Leaflet / MapLibre.
    /// </summary>
    [HttpGet("{capa}/geojson")]
    [ProducesResponseType(typeof(GeoJsonFeatureCollectionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerGeoJson(
        string capa,
        [FromQuery] double? minX = null,
        [FromQuery] double? minY = null,
        [FromQuery] double? maxX = null,
        [FromQuery] double? maxY = null,
        [FromQuery] int limit = 2000,
        CancellationToken cancellationToken = default)
    {
        var geojson = await _cadastreRepository.GetLayerGeoJsonAsync(capa, minX, minY, maxX, maxY, limit, cancellationToken);
        return Ok(geojson);
    }

    /// <summary>
    /// Identifica entidades geográficas bajo un punto de clic en el mapa (CU17 - Identificar Elemento).
    /// </summary>
    [HttpGet("identificar")]
    [ProducesResponseType(typeof(IReadOnlyList<GeoJsonFeatureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Identificar(
        [FromQuery] double lng,
        [FromQuery] double lat,
        [FromQuery] double tolerancia = 10,
        CancellationToken cancellationToken = default)
    {
        var elementos = await _cadastreRepository.IdentifyAsync(lng, lat, tolerancia, cancellationToken);
        return Ok(elementos);
    }

    /// <summary>
    /// Obtiene la extensión total (bounding box [minX, minY, maxX, maxY]) para zoom automático (CU19 - Gestionar Extensión).
    /// </summary>
    [HttpGet("extension")]
    [ProducesResponseType(typeof(double[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerExtension([FromQuery] string? capa = null, CancellationToken cancellationToken = default)
    {
        var bbox = await _cadastreRepository.GetLayerExtentAsync(capa, cancellationToken);
        if (bbox is null)
        {
            return NotFound(new { mensaje = "No se encontraron geometrías para calcular la extensión." });
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
}
