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
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IEmpleadoRepository _empleadoRepository;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        ICadastreRepository cadastreRepository,
        IBitacoraService bitacoraService,
        IUsuarioRepository usuarioRepository,
        IEmpleadoRepository empleadoRepository,
        ILogger<DashboardController> logger)
    {
        _cadastreRepository = cadastreRepository ?? throw new ArgumentNullException(nameof(cadastreRepository));
        _bitacoraService = bitacoraService ?? throw new ArgumentNullException(nameof(bitacoraService));
        _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        _empleadoRepository = empleadoRepository ?? throw new ArgumentNullException(nameof(empleadoRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Consulta los indicadores consolidados para el Dashboard principal del administrador.
    /// </summary>
    [HttpGet("indicadores")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerIndicadores(CancellationToken cancellationToken)
    {
        var capas = await _cadastreRepository.GetLayersCatalogAsync(cancellationToken);
        var bitacora = await _bitacoraService.ObtenerHistorialAsync(10, cancellationToken);
        var usuarios = (await _usuarioRepository.ObtenerTodosAsync(cancellationToken)).ToList();
        var empleados = await _empleadoRepository.ObtenerTodosAsync(null, null, null, null, cancellationToken);

        var totalManzanas = ObtenerTotalCapa(capas, "Manzanas");
        var totalLotes = ObtenerTotalCapa(capas, "Lotes");
        var totalCodigosFijos = ObtenerTotalCapa(capas, "CodigosFijos");
        var totalVias = ObtenerTotalCapa(capas, "Vias");
        var totalRegistrosSIG = totalManzanas + totalLotes + totalCodigosFijos + totalVias;

        var empleadosLista = empleados.ToList();
        var totalEmpleados = empleadosLista.Count;
        var empleadosDisponibles = empleadosLista.Count(e => string.Equals(e.Disponibilidad, "Disponible", StringComparison.OrdinalIgnoreCase));
        var empleadosEnServicio = empleadosLista.Count(e => string.Equals(e.Disponibilidad, "En Servicio", StringComparison.OrdinalIgnoreCase));
        var empleadosBajas = empleadosLista.Count(e => string.Equals(e.Disponibilidad, "Baja Parcial", StringComparison.OrdinalIgnoreCase));

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

        var listaUsuariosResumen = usuarios.Select(u => new
        {
            u.IdUsuario,
            u.Login,
            u.Nombre,
            u.Activo,
            u.Roles
        });

        var listaEmpleadosResumen = empleadosLista.Select(e => new
        {
            e.IdEmpleado,
            e.Codigo,
            e.NombreCompleto,
            e.Cargo,
            e.Area,
            e.Disponibilidad,
            e.Activo
        });

        return Ok(new
        {
            kpis = new
            {
                totalUsuarios = usuarios.Count,
                usuariosConectados = usuarios.Count(u => u.Activo),
                usuariosDesconectados = usuarios.Count(u => !u.Activo),
                totalEmpleados,
                empleadosDisponibles,
                empleadosEnServicio,
                empleadosBajas,
                totalRegistrosSIG,
                totalManzanas,
                totalLotes,
                totalCodigosFijos,
                totalVias
            },
            capas,
            usuarios = listaUsuariosResumen,
            empleados = listaEmpleadosResumen,
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
