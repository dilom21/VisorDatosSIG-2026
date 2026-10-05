using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.DTOs.Lotes;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Busqueda paginada y detalle espacial de lotes (CU15).
/// </summary>
/// <remarks>
/// El detalle conserva la geometria completa de <c>dbo.Lotes.Geom</c> como GeoJSON
/// Polygon/MultiPolygon. No calcula centroides ni coordenadas independientes.
/// </remarks>
[Authorize]
[ApiController]
[Route("api/lotes")]
[Produces("application/json")]
public sealed class LotesController : ControllerBase
{
    private const int PaginaPorDefecto = 1;
    private const int LimitePorDefecto = 20;
    private const int LimiteMaximo = 100;
    private const string AccionBitacora = "Consultar Lote";

    private readonly ICadastreRepository _cadastreRepository;
    private readonly IPermisoService _permisoService;
    private readonly IBitacoraService _bitacoraService;
    private readonly ILogger<LotesController> _logger;

    public LotesController(
        ICadastreRepository cadastreRepository,
        IPermisoService permisoService,
        IBitacoraService bitacoraService,
        ILogger<LotesController> logger)
    {
        _cadastreRepository = cadastreRepository ?? throw new ArgumentNullException(nameof(cadastreRepository));
        _permisoService = permisoService ?? throw new ArgumentNullException(nameof(permisoService));
        _bitacoraService = bitacoraService ?? throw new ArgumentNullException(nameof(bitacoraService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Consulta lotes con filtros combinables y paginación del servidor.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LoteResumenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? nroLote = null,
        [FromQuery] int? idManzana = null,
        [FromQuery] int pagina = PaginaPorDefecto,
        [FromQuery] int limite = LimitePorDefecto,
        CancellationToken cancellationToken = default)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);
        if (denegado is not null)
        {
            return denegado;
        }

        var errorValidacion = ValidarPaginacion(pagina, limite);
        if (errorValidacion is not null)
        {
            return errorValidacion;
        }

        if (idManzana.HasValue && idManzana.Value < 1)
            return Problema(400, "Consulta invalida", "idManzana debe ser mayor o igual a 1.");

        var consulta = new LoteConsultaDto
        {
            NroLote = Normalizar(nroLote),
            IdManzana = idManzana,
            Pagina = pagina,
            Limite = limite
        };

        var resultado = await _cadastreRepository.SearchLotesAsync(consulta, cancellationToken);

        await RegistrarBitacoraAsync(
            idEntidad: null,
            $"Busqueda de lotes. Pagina: {pagina}; limite: {limite}; " +
            $"coincidencias: {resultado.TotalRegistros}.",
            cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Obtiene un lote por IdLote con su geometria GeoJSON Polygon/MultiPolygon.
    /// </summary>
    /// <remarks>
    /// Las coordenadas se conservan como <c>[longitud, latitud]</c>. La geometria queda preparada
    /// para dibujar, resaltar u obtener bounds en una integracion futura con el visor.
    /// </remarks>
    [HttpGet("{idLote:int}")]
    [ProducesResponseType(typeof(LoteDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(
        int idLote,
        CancellationToken cancellationToken = default)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);
        if (denegado is not null)
        {
            return denegado;
        }

        if (idLote < 1) return Problema(400, "Consulta invalida", "El identificador debe ser positivo.");

        var lote = await _cadastreRepository.GetLoteByIdAsync(idLote, cancellationToken);
        if (lote is null)
        {
            return Problema(
                StatusCodes.Status404NotFound,
                "Lote no encontrada",
                $"No existe un lote con IdLote {idLote}.");
        }

        await RegistrarBitacoraAsync(
            idLote,
            $"Consulta de el lote con IdLote {idLote}.",
            cancellationToken);

        return Ok(lote);
    }

    private async Task<IActionResult?> VerificarAccesoAsync(CancellationToken cancellationToken)
    {
        var idUsuario = User.IdUsuario();
        if (idUsuario is null)
        {
            return Unauthorized();
        }

        if (!await User.PuedeVerAsync(
                _permisoService,
                PermisoMenu.ConsultaLotes,
                cancellationToken))
        {
            _logger.LogWarning(
                "Acceso denegado a lotes: el usuario {IdUsuario} no tiene permiso sobre {Menu}.",
                idUsuario.Value,
                PermisoMenu.ConsultaLotes);

            return Problema(
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                "No tiene permiso para consultar lotes.");
        }

        return null;
    }

    private async Task RegistrarBitacoraAsync(
        long? idEntidad,
        string detalle,
        CancellationToken cancellationToken)
    {
        try
        {
            var registrado = await _bitacoraService.RegistrarAsync(new BitacoraEntryDto
            {
                IdUsuario = User.IdUsuario(),
                FechaHora = DateTime.UtcNow,
                Modulo = ModulosSistema.ConsultasYFiltros,
                Accion = AccionBitacora,
                Entidad = "Lotes",
                IdEntidad = idEntidad,
                Resultado = "EXITO",
                Detalle = detalle,
                IP = HttpContext.IpOrigen()
            }, cancellationToken);

            if (!registrado)
            {
                _logger.LogWarning(
                    "No se pudo registrar en bitacora la consulta de lote {IdLote}.",
                    idEntidad);
            }
        }
        catch (Exception excepcion)
        {
            _logger.LogWarning(
                excepcion,
                "Fallo no bloqueante al registrar en bitacora la consulta de lote {IdLote}.",
                idEntidad);
        }
    }

    private ObjectResult? ValidarPaginacion(int pagina, int limite)
    {
        if (pagina < 1)
        {
            return Problema(StatusCodes.Status400BadRequest, "Consulta invalida", "pagina debe ser mayor o igual a 1.");
        }

        if (limite < 1 || limite > LimiteMaximo)
        {
            return Problema(StatusCodes.Status400BadRequest, "Consulta invalida", "limite debe estar entre 1 y 100.");
        }

        return null;
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private ObjectResult Problema(int estado, string titulo, string? detalle) =>
        StatusCode(estado, new ProblemDetails
        {
            Status = estado,
            Title = titulo,
            Detail = detalle
        });
}
