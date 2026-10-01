using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Api.Authorization;
using VisorDatosSIG.Application.DTOs.Navigation;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Menú lateral dinámico del sistema (CU01 - Menú de navegación).
/// </summary>
/// <remarks>
/// El menú se lee de <c>dbo.MenuOpciones</c> y se devuelve anidado (padres con sus hijos) para que
/// el VisorDatosSIG.Web lo renderice sin nombres hardcodeados. La jerarquía y el filtrado por
/// permisos se resuelven en el servicio: el frontend solo sabe dibujar una colección de opciones.
/// </remarks>
[Authorize]
[ApiController]
[Route("api/menu")]
[Produces("application/json")]
public sealed class MenuController : ControllerBase
{
    private readonly IMenuService _menuService;

    /// <summary>
    /// Inicializa el controlador con el servicio del menú.
    /// </summary>
    public MenuController(IMenuService menuService) =>
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));

    /// <summary>
    /// Devuelve el menú lateral del usuario autenticado: solo las opciones que sus roles activos
    /// pueden ver, ordenadas y jerárquicas.
    /// </summary>
    /// <remarks>
    /// Requiere token JWT válido. El identificador del usuario se toma del token, nunca del cuerpo
    /// ni de la cadena de consulta. Un usuario cuyos roles no habilitan el visor web (por ejemplo,
    /// solo el rol del migrador) recibe <c>403</c>.
    /// </remarks>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Menú de opciones visibles (arreglo vacío si ningún rol otorga opciones).</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    /// <response code="403">El usuario no tiene roles habilitados para usar el visor web.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MenuOpcionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
    {
        var idUsuario = User.IdUsuario();

        if (idUsuario is null)
        {
            return Unauthorized();
        }

        var menu = await _menuService.ObtenerMenuAsync(idUsuario.Value, cancellationToken);

        if (!menu.TieneAccesoWeb)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Acceso denegado",
                Detail = "El usuario no tiene roles habilitados para usar el visor web."
            });
        }

        return Ok(menu.Opciones);
    }
}
