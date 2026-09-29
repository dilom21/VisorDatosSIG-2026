using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class CatastroController : ControllerBase
{
    private readonly ICadastreRepository _cadastreRepository;
    private readonly ILogger<CatastroController> _logger;

    public CatastroController(ICadastreRepository cadastreRepository, ILogger<CatastroController> logger)
    {
        _cadastreRepository = cadastreRepository ?? throw new ArgumentNullException(nameof(cadastreRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Búsqueda unificada alfanumérica en capas catastrales (CU08, CU09, CU10, CU11, CU13).
    /// Permite buscar por texto libre en UV, Manzana, Lote, Código Fijo, Nombre de Cliente o Vía.
    /// </summary>
    [HttpGet("buscar")]
    [ProducesResponseType(typeof(PagedResult<IDictionary<string, object?>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Buscar(
        [FromQuery] string q,
        [FromQuery] string? capa = null,
        [FromQuery] int pagina = 1,
        [FromQuery] int limite = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { mensaje = "El parámetro de búsqueda 'q' no puede estar vacío." });
        }

        var resultados = await _cadastreRepository.SearchAsync(q, capa, pagina, limite, cancellationToken);
        return Ok(resultados);
    }
}
