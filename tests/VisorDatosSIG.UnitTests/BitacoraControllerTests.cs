using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Bitacora;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas del controlador de consulta de bitácora (<c>/api/bitacora</c>): contrato HTTP,
/// autorización por permiso y respuesta ante eventos inexistentes.
/// </summary>
public sealed class BitacoraControllerTests
{
    private sealed class BitacoraConsultaServiceFalso : IBitacoraConsultaService
    {
        public BitacoraConsultaDto? ConsultaRecibida { get; private set; }

        public long? IdRecibido { get; private set; }

        public Task<BitacoraPaginaDto> ConsultarAsync(
            BitacoraConsultaDto consulta,
            CancellationToken cancellationToken = default)
        {
            ConsultaRecibida = consulta;

            return Task.FromResult(new BitacoraPaginaDto
            {
                Pagina = consulta.Pagina,
                Tamano = consulta.Tamano,
                TotalRegistros = 3,
                Datos =
                [
                    new BitacoraItemWebDto
                    {
                        IdBitacora = 12,
                        FechaHora = new DateTime(2026, 3, 1, 9, 30, 0, DateTimeKind.Unspecified),
                        Modulo = ModulosSistema.UsuariosYSeguridad,
                        Accion = "Inicio de Sesión",
                        Resultado = "EXITOSO"
                    }
                ]
            });
        }

        public Task<BitacoraCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new BitacoraCatalogosDto
            {
                Modulos = [ModulosSistema.UsuariosYSeguridad],
                Resultados = ["EXITOSO"]
            });

        public Task<BitacoraItemWebDto?> ObtenerPorIdAsync(
            long idBitacora,
            CancellationToken cancellationToken = default)
        {
            IdRecibido = idBitacora;

            return Task.FromResult<BitacoraItemWebDto?>(null);
        }
    }

    private sealed class PermisoServiceFalso(bool puedeVer) : IPermisoService
    {
        public Task<PermisosEfectivosDto> ObtenerPermisosAsync(
            int idUsuario,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PermisosEfectivosDto { TieneAccesoWeb = puedeVer });

        public Task<bool> TieneAccesoWebAsync(int idUsuario, CancellationToken cancellationToken = default) =>
            Task.FromResult(puedeVer);

        public Task<bool> TienePermisoAsync(
            int idUsuario,
            string menuUrl,
            AccionPermiso accion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(puedeVer);

        public Task<bool> PuedeVerAsync(int idUsuario, string menuUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult(puedeVer);
    }

    /// <summary>Construye la identidad del token JWT para el usuario indicado (claim <c>sub</c>).</summary>
    private static ClaimsPrincipal Principal(int? idUsuario)
    {
        var reclamos = idUsuario.HasValue
            ? new[] { new Claim("sub", idUsuario.Value.ToString(CultureInfo.InvariantCulture)) }
            : [];

        return new ClaimsPrincipal(new ClaimsIdentity(reclamos, authenticationType: "Prueba"));
    }

    private static BitacoraController Controlador(
        BitacoraConsultaServiceFalso servicio,
        bool puedeVer,
        int? idUsuario = 9) =>
        new(servicio, new PermisoServiceFalso(puedeVer), NullLogger<BitacoraController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(idUsuario) }
            }
        };

    [Fact(DisplayName = "Bitácora API: expone las rutas de consulta y exige autenticación")]
    public void ExponeLasRutasYExigeAutenticacion()
    {
        var tipo = typeof(BitacoraController);

        Assert.NotNull(tipo.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(tipo.GetCustomAttribute<ApiControllerAttribute>());

        var ruta = tipo.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(ruta);
        Assert.Equal("api/bitacora", ruta!.Template);

        Assert.NotNull(tipo.GetMethod(nameof(BitacoraController.Consultar))!.GetCustomAttribute<HttpGetAttribute>());
        Assert.Equal(
            "catalogos",
            tipo.GetMethod(nameof(BitacoraController.ObtenerCatalogos))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal(
            "{idBitacora:long}",
            tipo.GetMethod(nameof(BitacoraController.Obtener))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    [Fact(DisplayName = "Bitácora API: 200 con la página de eventos cuando hay permiso de consulta")]
    public async Task DevuelveLaPaginaConPermiso()
    {
        var servicio = new BitacoraConsultaServiceFalso();

        var resultado = await Controlador(servicio, puedeVer: true)
            .Consultar(new BitacoraConsultaDto { Pagina = 2, Tamano = 10 }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var pagina = Assert.IsType<BitacoraPaginaDto>(ok.Value);

        Assert.Equal(2, pagina.Pagina);
        Assert.Equal(3, pagina.TotalRegistros);
        Assert.Equal("Inicio de Sesión", Assert.Single(pagina.Datos).Accion);
        Assert.Equal(2, servicio.ConsultaRecibida!.Pagina);
    }

    [Fact(DisplayName = "Bitácora API: 403 cuando falta el permiso del módulo de bitácora")]
    public async Task DevuelveProhibidoSinPermiso()
    {
        var servicio = new BitacoraConsultaServiceFalso();

        var resultado = await Controlador(servicio, puedeVer: false)
            .Consultar(new BitacoraConsultaDto(), CancellationToken.None);

        var prohibido = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, prohibido.StatusCode);
        Assert.IsType<ProblemDetails>(prohibido.Value);
        Assert.Null(servicio.ConsultaRecibida);
    }

    [Fact(DisplayName = "Bitácora API: 401 cuando el token no identifica al usuario")]
    public async Task DevuelveNoAutorizadoSinIdentidad()
    {
        var resultado = await Controlador(new BitacoraConsultaServiceFalso(), puedeVer: true, idUsuario: null)
            .Consultar(new BitacoraConsultaDto(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    [Fact(DisplayName = "Bitácora API: los catálogos se devuelven solo con permiso de consulta")]
    public async Task DevuelveLosCatalogos()
    {
        var conPermiso = await Controlador(new BitacoraConsultaServiceFalso(), puedeVer: true)
            .ObtenerCatalogos(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(conPermiso);
        var catalogos = Assert.IsType<BitacoraCatalogosDto>(ok.Value);
        Assert.Equal(ModulosSistema.UsuariosYSeguridad, Assert.Single(catalogos.Modulos));

        var sinPermiso = await Controlador(new BitacoraConsultaServiceFalso(), puedeVer: false)
            .ObtenerCatalogos(CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(sinPermiso).StatusCode);
    }

    [Fact(DisplayName = "Bitácora API: 404 cuando el evento no existe o es del migrador")]
    public async Task DevuelveNoEncontradoParaElEventoInvisible()
    {
        var servicio = new BitacoraConsultaServiceFalso();

        var resultado = await Controlador(servicio, puedeVer: true).Obtener(88, CancellationToken.None);

        var noEncontrado = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status404NotFound, noEncontrado.StatusCode);
        Assert.IsType<ProblemDetails>(noEncontrado.Value);
        Assert.Equal(88, servicio.IdRecibido);
    }
}
