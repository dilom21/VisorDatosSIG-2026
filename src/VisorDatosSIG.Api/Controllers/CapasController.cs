using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Controlador de servicios geoespaciales para el Visor Cartográfico (CU06 al CU11).
/// Soporta CU09 - Gestionar Elementos Geográfico, visibilidad, leyendas e identificación.
/// Actores: Administrador, Consultor.
/// </summary>
[Authorize(Roles = "Administrador,Consultor")]
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
    private readonly ILogger<CapasController> _logger;

    public CapasController(
        ICadastreRepository cadastreRepository,
        IBitacoraService bitacoraService,
        ILogger<CapasController> logger)
    {
        _cadastreRepository = cadastreRepository ?? throw new ArgumentNullException(nameof(cadastreRepository));
        _bitacoraService = bitacoraService ?? throw new ArgumentNullException(nameof(bitacoraService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private int? ObtenerIdUsuario()
    {
        var claimSub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(claimSub, out var id) ? id : null;
    }

    private string? ObtenerIpOrigen() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

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
    /// Obtiene las geometrías de una capa en formato GeoJSON RFC 7946 (CU09 - Gestionar Elementos Geográfico, RF-VIS-02, RF-VIS-06).
    /// Soporta Bounding Box (minX, minY, maxX, maxY) para optimización en Leaflet.
    /// El parámetro limit se acota entre 1 y 10000 para evitar sobrecarga del servidor.
    /// </summary>
    [HttpGet("{capa}/geojson")]
    [ProducesResponseType(typeof(GeoJsonFeatureCollectionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerGeoJson(
        string capa,
        [FromQuery] double? minX = null,
        [FromQuery] double? minY = null,
        [FromQuery] double? maxX = null,
        [FromQuery] double? maxY = null,
        [FromQuery, Range(LimiteMinimo, LimiteMaximo)] int limit = LimitePorDefecto,
        CancellationToken cancellationToken = default)
    {
        var limitAcotado = Math.Clamp(limit <= 0 ? LimitePorDefecto : limit, LimiteMinimo, LimiteMaximo);
        var geojson = await _cadastreRepository.GetLayerGeoJsonAsync(capa, minX, minY, maxX, maxY, limitAcotado, cancellationToken);

        // Registro de auditoría (CU09 - Gestionar Elementos Geográfico, Módulo: Visor Cartográfico)
        try
        {
            await _bitacoraService.RegistrarAsync(new BitacoraEntryDto
            {
                IdUsuario = ObtenerIdUsuario(),
                FechaHora = DateTime.UtcNow,
                Modulo = ModulosSistema.VisorCartografico,
                Accion = "Gestionar Elementos Geográfico",
                Entidad = capa,
                Resultado = "EXITO",
                Detalle = $"Visualización de elementos geográficos de capa '{capa}'. Total elementos: {geojson.Features.Count}. Límite: {limitAcotado}.",
                IP = ObtenerIpOrigen()
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
    [HttpGet("identificar")]
    [ProducesResponseType(typeof(IReadOnlyList<GeoJsonFeatureDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Identificar(
        [FromQuery] double lng,
        [FromQuery] double lat,
        [FromQuery] double tolerancia = 10,
        CancellationToken cancellationToken = default)
    {
        var elementos = await _cadastreRepository.IdentifyAsync(lng, lat, tolerancia, cancellationToken);

        // Registro de auditoría (CU10 - Gestionar Entidades, Módulo: Visor Cartográfico)
        try
        {
            await _bitacoraService.RegistrarAsync(new BitacoraEntryDto
            {
                IdUsuario = ObtenerIdUsuario(),
                FechaHora = DateTime.UtcNow,
                Modulo = ModulosSistema.VisorCartografico,
                Accion = "Gestionar Entidades",
                Entidad = "Identificación",
                Resultado = "EXITO",
                Detalle = $"Identificación de entidades en ({lng:F6}, {lat:F6}) con tolerancia {tolerancia}m. Coincidencias encontradas: {elementos.Count}.",
                IP = ObtenerIpOrigen()
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
    [HttpGet("extension")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerExtension([FromQuery] string? capa = null, CancellationToken cancellationToken = default)
    {
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
                IdUsuario = ObtenerIdUsuario(),
                FechaHora = DateTime.UtcNow,
                Modulo = ModulosSistema.VisorCartografico,
                Accion = "Gestionar Extensión",
                Entidad = string.IsNullOrWhiteSpace(capa) ? "General" : capa,
                Resultado = "EXITO",
                Detalle = string.IsNullOrWhiteSpace(capa)
                    ? $"Cálculo de extensión general: [{bbox[0]:F6}, {bbox[1]:F6}, {bbox[2]:F6}, {bbox[3]:F6}]."
                    : $"Cálculo de extensión para capa '{capa}': [{bbox[0]:F6}, {bbox[1]:F6}, {bbox[2]:F6}, {bbox[3]:F6}].",
                IP = ObtenerIpOrigen()
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
}
