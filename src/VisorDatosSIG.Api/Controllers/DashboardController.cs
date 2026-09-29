using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class DashboardController : ControllerBase
{
    private readonly ICadastreRepository _cadastreRepository;
    private readonly IBitacoraService _bitacoraService;
    private readonly IAuthRepository _authRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        ICadastreRepository cadastreRepository,
        IBitacoraService bitacoraService,
        IAuthRepository authRepository,
        ILogger<DashboardController> logger)
    {
        _cadastreRepository = cadastreRepository ?? throw new ArgumentNullException(nameof(cadastreRepository));
        _bitacoraService = bitacoraService ?? throw new ArgumentNullException(nameof(bitacoraService));
        _authRepository = authRepository ?? throw new ArgumentNullException(nameof(authRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Consulta los indicadores consolidados para el Dashboard principal (CU31, CU32, CU33, CU34).
    /// </summary>
    [HttpGet("indicadores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerIndicadores(CancellationToken cancellationToken)
    {
        var capas = await _cadastreRepository.GetLayersCatalogAsync(cancellationToken);
        var bitacora = await _bitacoraService.ObtenerHistorialAsync(10, cancellationToken);
        var usuarios = await _authRepository.GetAllUsersAsync(cancellationToken);

        var totalManzanas = ObtenerTotalCapa(capas, "Manzanas");
        var totalLotes = ObtenerTotalCapa(capas, "Lotes");
        var totalCodigosFijos = ObtenerTotalCapa(capas, "CodigosFijos");
        var totalVias = ObtenerTotalCapa(capas, "Vias");

        var ultimasOperaciones = bitacora.Select(b => new
        {
            b.IdBitacora,
            b.FechaHora,
            b.Modulo,
            b.Accion,
            b.Entidad,
            b.Resultado,
            b.IdUsuario
        });

        return Ok(new
        {
            kpis = new
            {
                totalManzanas,
                totalLotes,
                totalCodigosFijos,
                totalVias,
                totalUsuarios = usuarios.Count,
                usuariosActivos = usuarios.Count(u => u.Activo)
            },
            capas,
            actividadReciente = ultimasOperaciones
        });
    }

    private static int ObtenerTotalCapa(IReadOnlyList<IDictionary<string, object?>> capas, string nombre)
    {
        var fila = capas.FirstOrDefault(c => string.Equals(c["Capa"]?.ToString(), nombre, StringComparison.OrdinalIgnoreCase));
        if (fila is not null && fila.TryGetValue("TotalRegistros", out var val) && val is not null)
        {
            return Convert.ToInt32(val);
        }
        return 0;
    }
}
