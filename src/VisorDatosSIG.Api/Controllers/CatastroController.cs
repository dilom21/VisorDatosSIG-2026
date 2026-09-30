using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class CatastroController : ControllerBase
{
    private const int LimiteMinimo = 1;
    private const int LimiteMaximo = 100;
    private const int LimitePorDefecto = 20;

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
    /// El parámetro limite se acota entre 1 y 100.
    /// </summary>
    [HttpGet("buscar")]
    [ProducesResponseType(typeof(PagedResult<IDictionary<string, object?>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Buscar(
        [FromQuery] string q,
        [FromQuery] string? capa = null,
        [FromQuery, Range(1, int.MaxValue)] int pagina = 1,
        [FromQuery, Range(LimiteMinimo, LimiteMaximo)] int limite = LimitePorDefecto,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { mensaje = "El parámetro de búsqueda 'q' no puede estar vacío." });
        }

        var paginaAcotada = Math.Max(1, pagina);
        var limiteAcotado = Math.Clamp(limite <= 0 ? LimitePorDefecto : limite, LimiteMinimo, LimiteMaximo);

        var resultados = await _cadastreRepository.SearchAsync(q, capa, paginaAcotada, limiteAcotado, cancellationToken);
        return Ok(resultados);
    }
}
