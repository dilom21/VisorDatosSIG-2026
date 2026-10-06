using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.DTOs.CodigosFijos;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Consulta paginada y detalle de codigos fijos (CU13).
/// </summary>
[Authorize]
[ApiController]
[Route("api/codigos-fijos")]
[Produces("application/json")]
public sealed class CodigosFijosController : ControllerBase
{
    private const int PaginaPorDefecto = 1;
    private const int LimitePorDefecto = 20;
    private const int LimiteMaximo = 100;
    private const string AccionBitacora = "Consultar Código Fijo";

    private readonly ICadastreRepository _cadastreRepository;
    private readonly IPermisoService _permisoService;
    private readonly IBitacoraService _bitacoraService;
    private readonly ILogger<CodigosFijosController> _logger;

    public CodigosFijosController(
        ICadastreRepository cadastreRepository,
        IPermisoService permisoService,
        IBitacoraService bitacoraService,
        ILogger<CodigosFijosController> logger)
    {
        _cadastreRepository = cadastreRepository ?? throw new ArgumentNullException(nameof(cadastreRepository));
        _permisoService = permisoService ?? throw new ArgumentNullException(nameof(permisoService));
        _bitacoraService = bitacoraService ?? throw new ArgumentNullException(nameof(bitacoraService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Busca codigos fijos aplicando todos los filtros informados y devuelve una pagina tipada.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CodigoFijoResumenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? codFSig = null,
        [FromQuery] int? codFijo = null,
        [FromQuery] string? nombre = null,
        [FromQuery] byte? estado = null,
        [FromQuery] int pagina = PaginaPorDefecto,
        [FromQuery] int limite = LimitePorDefecto,
        CancellationToken cancellationToken = default)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);
        if (denegado is not null)
        {
            return denegado;
        }

        var errorValidacion = ValidarConsulta(pagina, limite, estado);
        if (errorValidacion is not null)
        {
            return errorValidacion;
        }

        var consulta = new CodigoFijoConsultaDto
        {
            CodFSig = Normalizar(codFSig),
            CodFijo = codFijo,
            Nombre = Normalizar(nombre),
            Estado = estado,
            Pagina = pagina,
            Limite = limite
        };

        var resultado = await _cadastreRepository.SearchCodigosFijosAsync(consulta, cancellationToken);

        await RegistrarBitacoraAsync(
            idEntidad: null,
            $"Busqueda de codigos fijos. Pagina: {pagina}; limite: {limite}; " +
            $"coincidencias: {resultado.TotalRegistros}.",
            cancellationToken);

        return Ok(resultado);
    }

    /// <summary>
    /// Obtiene un codigo fijo por IdCodigo, incluida su geometria como GeoJSON.
    /// </summary>
    [HttpGet("{idCodigo:int}")]
    [ProducesResponseType(typeof(CodigoFijoDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(
        int idCodigo,
        CancellationToken cancellationToken = default)
    {
        var denegado = await VerificarAccesoAsync(cancellationToken);
        if (denegado is not null)
        {
            return denegado;
        }

        var codigoFijo = await _cadastreRepository.GetCodigoFijoByIdAsync(idCodigo, cancellationToken);
        if (codigoFijo is null)
        {
            return Problema(
                StatusCodes.Status404NotFound,
                "Codigo fijo no encontrado",
                $"No existe un codigo fijo con IdCodigo {idCodigo}.");
        }

        await RegistrarBitacoraAsync(
            idCodigo,
            $"Consulta del codigo fijo con IdCodigo {idCodigo}.",
            cancellationToken);

        return Ok(codigoFijo);
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
                PermisoMenu.ConsultaCodigoFijo,
                cancellationToken))
        {
            _logger.LogWarning(
                "Acceso denegado a codigos fijos: el usuario {IdUsuario} no tiene permiso sobre {Menu}.",
                idUsuario.Value,
                PermisoMenu.ConsultaCodigoFijo);

            return Problema(
                StatusCodes.Status403Forbidden,
                "Acceso denegado",
                "No tiene permiso para consultar codigos fijos.");
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
                Entidad = "CodigosFijos",
                IdEntidad = idEntidad,
                Resultado = "EXITO",
                Detalle = detalle,
                IP = HttpContext.IpOrigen()
            }, cancellationToken);

            if (!registrado)
            {
                _logger.LogWarning(
                    "No se pudo registrar en bitacora la consulta de codigo fijo {IdCodigo}.",
                    idEntidad);
            }
        }
        catch (Exception excepcion)
        {
            _logger.LogWarning(
                excepcion,
                "Fallo no bloqueante al registrar en bitacora la consulta de codigo fijo {IdCodigo}.",
                idEntidad);
        }
    }

    private ObjectResult? ValidarConsulta(int pagina, int limite, byte? estado)
    {
        if (pagina < 1)
        {
            return Problema(StatusCodes.Status400BadRequest, "Consulta invalida", "pagina debe ser mayor o igual a 1.");
        }

        if (limite < 1 || limite > LimiteMaximo)
        {
            return Problema(StatusCodes.Status400BadRequest, "Consulta invalida", "limite debe estar entre 1 y 100.");
        }

        if (estado is < 1 or > 5)
        {
            return Problema(StatusCodes.Status400BadRequest, "Consulta invalida", "estado debe estar entre 1 y 5.");
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
