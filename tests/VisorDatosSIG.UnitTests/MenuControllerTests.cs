using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.DTOs.Navigation;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas del controlador del menú lateral dinámico (<c>GET /api/menu</c>): contrato,
/// protección por token y ruta pública del endpoint.
/// </summary>
public sealed class MenuControllerTests
{
    private sealed class MenuServiceFalso(MenuUsuarioDto menu) : IMenuService
    {
        public int? IdUsuarioRecibido { get; private set; }

        public CancellationToken TokenRecibido { get; private set; }

        public Task<MenuUsuarioDto> ObtenerMenuAsync(int idUsuario, CancellationToken cancellationToken = default)
        {
            IdUsuarioRecibido = idUsuario;
            TokenRecibido = cancellationToken;
            return Task.FromResult(menu);
        }
    }

    /// <summary>
    /// Construye la identidad del token JWT para el usuario indicado. La API usa
    /// <c>MapInboundClaims = false</c>, por lo que el identificador llega en el claim <c>sub</c>.
    /// </summary>
    private static ClaimsPrincipal Principal(int? idUsuario)
    {
        var reclamos = idUsuario.HasValue
            ? new[] { new Claim("sub", idUsuario.Value.ToString(CultureInfo.InvariantCulture)) }
            : [];

        return new ClaimsPrincipal(new ClaimsIdentity(reclamos, authenticationType: "Prueba"));
    }

    private static MenuController ControladorCon(MenuUsuarioDto menu, int? idUsuario)
    {
        var servicio = new MenuServiceFalso(menu);

        return new MenuController(servicio)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(idUsuario) }
            }
        };
    }

    private static MenuOpcionDto Opcion(int idMenu, string nombre, string? url = null) => new()
    {
        IdMenu = idMenu,
        NombreMenu = nombre,
        Url = url,
        Orden = idMenu
    };

    [Fact(DisplayName = "Menú API: devuelve 200 con las opciones del usuario identificado en el token")]
    public async Task DevuelveOkConElMenuDelServicio()
    {
        var servicio = new MenuServiceFalso(new MenuUsuarioDto
        {
            TieneAccesoWeb = true,
            Opciones = [Opcion(1, "Usuarios y seguridad")]
        });

        var controlador = new MenuController(servicio)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(12) }
            }
        };

        var resultado = await controlador.Obtener(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var menu = Assert.IsAssignableFrom<IReadOnlyList<MenuOpcionDto>>(ok.Value);
        var unico = Assert.Single(menu);
        Assert.Equal("Usuarios y seguridad", unico.NombreMenu);
        Assert.Equal(12, servicio.IdUsuarioRecibido);
    }

    [Fact(DisplayName = "Menú API: 403 cuando el usuario no tiene roles habilitados para el visor web")]
    public async Task DevuelveProhibidoSinAccesoWeb()
    {
        var controlador = ControladorCon(MenuUsuarioDto.SinAccesoWeb(), 12);

        var resultado = await controlador.Obtener(CancellationToken.None);

        var prohibido = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, prohibido.StatusCode);
        Assert.IsType<ProblemDetails>(prohibido.Value);
    }

    [Fact(DisplayName = "Menú API: 401 cuando el token no identifica al usuario")]
    public async Task DevuelveNoAutorizadoSinIdentidad()
    {
        var controlador = ControladorCon(
            new MenuUsuarioDto { TieneAccesoWeb = true, Opciones = [Opcion(1, "Reportes", "/Reportes")] },
            null);

        var resultado = await controlador.Obtener(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    [Fact(DisplayName = "Menú API: el controlador está en la ruta api/menu y exige autenticación")]
    public void ExponeLaRutaYExigeAutenticacion()
    {
        var tipo = typeof(MenuController);

        Assert.NotNull(tipo.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(tipo.GetCustomAttribute<ApiControllerAttribute>());

        var ruta = tipo.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(ruta);
        Assert.Equal("api/menu", ruta!.Template);

        var accion = tipo.GetMethod(nameof(MenuController.Obtener));
        Assert.NotNull(accion);
        Assert.NotNull(accion!.GetCustomAttribute<HttpGetAttribute>());
    }
}
