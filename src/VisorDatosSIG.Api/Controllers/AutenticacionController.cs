using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Authentication;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Autenticación de usuarios del VisorDatosSIG.
/// </summary>
/// <remarks>
/// El token devuelto por <c>POST /api/autenticacion/iniciar</c> debe enviarse en la cabecera
/// <c>Authorization: Bearer &lt;token&gt;</c> para acceder a los endpoints protegidos.
/// </remarks>
[ApiController]
[Route("api/autenticacion")]
[Produces("application/json")]
public sealed class AutenticacionController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    /// <summary>
    /// Inicializa el controlador con el servicio de autenticación.
    /// </summary>
    public AutenticacionController(IAuthenticationService authenticationService) =>
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));

    /// <summary>
    /// Inicia sesión y devuelve el token JWT de acceso.
    /// </summary>
    /// <param name="solicitud">Login y contraseña del usuario.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <response code="200">Credenciales válidas: devuelve el token y los datos públicos del usuario.</response>
    /// <response code="400">Datos incompletos o inválidos.</response>
    /// <response code="401">Credenciales incorrectas.</response>
    /// <response code="403">El usuario existe pero está inactivo.</response>
    [HttpPost("iniciar")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Iniciar(
        [FromBody] LoginRequestDto solicitud,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var resultado = await _authenticationService.IniciarSesionAsync(
            solicitud,
            ObtenerIpOrigen(),
            cancellationToken);

        if (resultado.IsSuccess && resultado.Response is not null)
        {
            return Ok(resultado.Response);
        }

        if (resultado.Status == AuthenticationStatus.InactiveUser)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Acceso denegado",
                Detail = "El usuario no está habilitado para iniciar sesión."
            });
        }

        // Mensaje genérico: no se revela si el login existe.
        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Credenciales inválidas",
            Detail = "El login o la contraseña no son correctos."
        });
    }

    /// <summary>
    /// Devuelve la información del usuario autenticado.
    /// </summary>
    /// <remarks>La identidad se obtiene exclusivamente del token JWT.</remarks>
    /// <response code="200">Datos públicos del usuario autenticado.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(AuthenticatedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var idUsuario = ObtenerIdUsuario();
        if (idUsuario is null)
        {
            return Unauthorized();
        }

        var login = User.FindFirstValue(JwtTokenService.ClaimLogin) ?? string.Empty;

        return Ok(new AuthenticatedUserDto
        {
            IdUsuario = idUsuario.Value,
            Login = login,
            Nombre = User.FindFirstValue(JwtTokenService.ClaimNombre) ?? login,
            Roles = User.FindAll(JwtTokenService.ClaimRol).Select(claim => claim.Value).ToArray()
        });
    }

    /// <summary>
    /// Registra el cierre de sesión del usuario autenticado.
    /// </summary>
    /// <remarks>
    /// No existe lista de revocación de tokens: el cliente debe eliminar su token de acceso.
    /// </remarks>
    /// <response code="204">Cierre de sesión registrado en la bitácora.</response>
    /// <response code="401">Token ausente, inválido o vencido.</response>
    [HttpPost("cerrar")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Cerrar(CancellationToken cancellationToken)
    {
        var idUsuario = ObtenerIdUsuario();
        if (idUsuario is null)
        {
            return Unauthorized();
        }

        await _authenticationService.CerrarSesionAsync(
            idUsuario.Value,
            User.FindFirstValue(JwtTokenService.ClaimLogin),
            ObtenerIpOrigen(),
            cancellationToken);

        return NoContent();
    }

    private int? ObtenerIdUsuario()
    {
        var valor = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return int.TryParse(valor, out var idUsuario) && idUsuario > 0 ? idUsuario : null;
    }

    private string? ObtenerIpOrigen()
    {
        var direccion = HttpContext.Connection.RemoteIpAddress;
        if (direccion is null)
        {
            return null;
        }

        return direccion.IsIPv4MappedToIPv6
            ? direccion.MapToIPv4().ToString()
            : direccion.ToString();
    }
}
