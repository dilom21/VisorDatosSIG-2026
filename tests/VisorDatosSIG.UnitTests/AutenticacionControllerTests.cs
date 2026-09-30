using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Authentication;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas del controlador de autenticación (rutas, protecciones e identidad desde el JWT).
/// </summary>
public sealed class AutenticacionControllerTests
{
    private sealed class AuthenticationServiceFalso(LoginResultDto resultado) : IAuthenticationService
    {
        public int VecesCerrado { get; private set; }

        public string? IpRecibida { get; private set; }

        public Task<LoginResultDto> IniciarSesionAsync(
            LoginRequestDto solicitud,
            string? ipOrigen = null,
            CancellationToken cancellationToken = default)
        {
            IpRecibida = ipOrigen;
            return Task.FromResult(resultado);
        }

        public Task<bool> CerrarSesionAsync(
            int idUsuario,
            string? login,
            string? ipOrigen = null,
            CancellationToken cancellationToken = default)
        {
            VecesCerrado++;
            IpRecibida = ipOrigen;
            return Task.FromResult(true);
        }
    }

    private static readonly string[] RolesAdministrador = ["Administrador"];

    private static AutenticacionController Crear(IAuthenticationService servicio, params Claim[] claims)
    {
        // Se configura el mismo ProblemDetailsFactory que usa la API en producción,
        // para que ValidationProblem genere el detalle con estado 400.
        var proveedor = new ServiceCollection()
            .AddOptions()
            .AddSingleton<ProblemDetailsFactory, DefaultProblemDetailsFactory>()
            .BuildServiceProvider();

        var contexto = new DefaultHttpContext
        {
            RequestServices = proveedor,
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Prueba"))
        };

        return new AutenticacionController(servicio)
        {
            ControllerContext = new ControllerContext { HttpContext = contexto }
        };
    }

    private static Claim[] ClaimsDeAdmin(int idUsuario = 1) =>
    [
        new(JwtRegisteredClaimNames.Sub, idUsuario.ToString()),
        new(JwtTokenService.ClaimLogin, "admin"),
        new(JwtTokenService.ClaimNombre, "Administrador"),
        new(JwtTokenService.ClaimRol, "Administrador")
    ];

    [Fact(DisplayName = "Endpoints: rutas y protecciones esperadas")]
    public void LosEndpointsTienenLasRutasYProteccionesEsperadas()
    {
        var tipo = typeof(AutenticacionController);

        Assert.Equal("api/autenticacion", tipo.GetCustomAttribute<RouteAttribute>()?.Template);

        var iniciar = tipo.GetMethod(nameof(AutenticacionController.Iniciar))!;
        Assert.Equal("iniciar", iniciar.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.NotNull(iniciar.GetCustomAttribute<AllowAnonymousAttribute>());

        var me = tipo.GetMethod(nameof(AutenticacionController.Me))!;
        Assert.Equal("me", me.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.NotNull(me.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Empty(me.GetParameters());

        var cerrar = tipo.GetMethod(nameof(AutenticacionController.Cerrar))!;
        Assert.Equal("cerrar", cerrar.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.NotNull(cerrar.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact(DisplayName = "GET /me: con token válido devuelve los datos públicos del usuario")]
    public void MeConTokenValidoDevuelveElUsuario()
    {
        var controller = Crear(new AuthenticationServiceFalso(LoginResultDto.Fallido(AuthenticationStatus.Success)), ClaimsDeAdmin());

        var resultado = controller.Me();

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var usuario = Assert.IsType<AuthenticatedUserDto>(ok.Value);

        Assert.Equal(1, usuario.IdUsuario);
        Assert.Equal("admin", usuario.Login);
        Assert.Equal("Administrador", usuario.Nombre);
        Assert.Equal(RolesAdministrador, usuario.Roles);
    }

    [Fact(DisplayName = "GET /me: sin token devuelve 401")]
    public void MeSinTokenDevuelveUnauthorized()
    {
        var controller = Crear(new AuthenticationServiceFalso(LoginResultDto.Fallido(AuthenticationStatus.Success)));

        Assert.IsType<UnauthorizedResult>(controller.Me());
    }

    [Fact(DisplayName = "GET /me: la identidad sale del JWT y no del cliente")]
    public void MeUsaLaIdentidadDelToken()
    {
        var controller = Crear(new AuthenticationServiceFalso(LoginResultDto.Fallido(AuthenticationStatus.Success)), ClaimsDeAdmin(42));

        var ok = Assert.IsType<OkObjectResult>(controller.Me());
        var usuario = Assert.IsType<AuthenticatedUserDto>(ok.Value);

        Assert.Equal(42, usuario.IdUsuario);
    }

    [Fact(DisplayName = "POST /iniciar: credenciales inválidas devuelven 401 con mensaje genérico")]
    public async Task IniciarConCredencialesInvalidasDevuelve401()
    {
        var controller = Crear(new AuthenticationServiceFalso(LoginResultDto.Fallido(AuthenticationStatus.InvalidCredentials)));

        var resultado = await controller.Iniciar(new LoginRequestDto { Login = "admin", Password = "mala" }, CancellationToken.None);

        var noAutorizado = Assert.IsType<UnauthorizedObjectResult>(resultado);
        Assert.Equal(401, noAutorizado.StatusCode);

        var problema = Assert.IsType<ProblemDetails>(noAutorizado.Value);
        Assert.DoesNotContain("existe", problema.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mala", problema.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "POST /iniciar: usuario inexistente también devuelve 401 genérico")]
    public async Task IniciarConUsuarioInexistenteDevuelve401()
    {
        var controller = Crear(new AuthenticationServiceFalso(LoginResultDto.Fallido(AuthenticationStatus.UserNotFound)));

        var resultado = await controller.Iniciar(new LoginRequestDto { Login = "fantasma", Password = "x" }, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(resultado);
    }

    [Fact(DisplayName = "POST /iniciar: usuario inactivo devuelve 403")]
    public async Task IniciarConUsuarioInactivoDevuelve403()
    {
        var controller = Crear(new AuthenticationServiceFalso(LoginResultDto.Fallido(AuthenticationStatus.InactiveUser)));

        var resultado = await controller.Iniciar(new LoginRequestDto { Login = "admin", Password = "Admin123!" }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(403, objectResult.StatusCode);
    }

    [Fact(DisplayName = "POST /iniciar: datos incompletos devuelven 400")]
    public async Task IniciarConDatosInvalidosDevuelve400()
    {
        var controller = Crear(new AuthenticationServiceFalso(LoginResultDto.Fallido(AuthenticationStatus.Success)));
        controller.ModelState.AddModelError(nameof(LoginRequestDto.Login), "El login es obligatorio.");

        var resultado = await controller.Iniciar(new LoginRequestDto(), CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(resultado);
        Assert.Equal(400, objectResult.StatusCode);

        var problema = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Equal(400, problema.Status);
    }

    [Fact(DisplayName = "POST /iniciar: credenciales válidas devuelven 200 con el token")]
    public async Task IniciarConCredencialesValidasDevuelve200()
    {
        var respuesta = new LoginResponseDto
        {
            AccessToken = "token",
            ExpiresIn = 1800,
            Usuario = new AuthenticatedUserDto
            {
                IdUsuario = 1,
                Login = "admin",
                Nombre = "Administrador",
                Roles = RolesAdministrador
            }
        };

        var controller = Crear(new AuthenticationServiceFalso(LoginResultDto.Exitoso(respuesta)));

        var resultado = await controller.Iniciar(new LoginRequestDto { Login = "admin", Password = "Admin123!" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        Assert.Same(respuesta, ok.Value);
    }

    [Fact(DisplayName = "POST /cerrar: devuelve 204 y usa el identificador del JWT")]
    public async Task CerrarDevuelve204YUsaElIdentificadorDelToken()
    {
        var servicio = new AuthenticationServiceFalso(LoginResultDto.Fallido(AuthenticationStatus.Success));
        var controller = Crear(servicio, ClaimsDeAdmin(7));

        var resultado = await controller.Cerrar(CancellationToken.None);

        Assert.IsType<NoContentResult>(resultado);
        Assert.Equal(1, servicio.VecesCerrado);
    }

    [Fact(DisplayName = "POST /cerrar: sin token devuelve 401")]
    public async Task CerrarSinTokenDevuelve401()
    {
        var servicio = new AuthenticationServiceFalso(LoginResultDto.Fallido(AuthenticationStatus.Success));
        var controller = Crear(servicio);

        Assert.IsType<UnauthorizedResult>(await controller.Cerrar(CancellationToken.None));
        Assert.Equal(0, servicio.VecesCerrado);
    }
}
