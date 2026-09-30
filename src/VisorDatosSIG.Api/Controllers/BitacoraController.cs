using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class BitacoraController : ControllerBase
{
    private readonly IBitacoraService _bitacoraService;
    private readonly ILogger<BitacoraController> _logger;

    public BitacoraController(IBitacoraService bitacoraService, ILogger<BitacoraController> logger)
    {
        _bitacoraService = bitacoraService ?? throw new ArgumentNullException(nameof(bitacoraService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Consulta el historial de eventos de la bitácora del sistema Web (CU05 - Consultar Bitácora).
    /// Por defecto excluye las operaciones técnicas del 'Migrador de Datos Geográficos'.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BitacoraItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerHistorial(
        [FromQuery] int limite = 100,
        [FromQuery] string? modulo = null,
        [FromQuery] string? tabla = null,
        [FromQuery] string? resultado = null,
        [FromQuery] bool excluirMigrador = true,
        CancellationToken cancellationToken = default)
    {
        var moduloExcluido = excluirMigrador && string.IsNullOrWhiteSpace(modulo)
            ? ModulosSistema.MigradorDeDatosGeograficos
            : null;

        var items = await _bitacoraService.ObtenerHistorialAsync(modulo, moduloExcluido, limite, cancellationToken);

        if (!string.IsNullOrWhiteSpace(modulo))
        {
            items = items.Where(x => x.Modulo.Contains(modulo, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(tabla))
        {
            items = items.Where(x => x.Entidad.Contains(tabla, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(resultado))
        {
            items = items.Where(x => x.Resultado.Equals(resultado, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return Ok(items);
    }
}
