using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas unitarias para CU09 - Gestionar Elementos Geográfico en CapasController.
/// </summary>
public sealed class CapasControllerTests
{
    private sealed class CadastreRepositoryFalso : ICadastreRepository
    {
        public string? UltimaCapaConsultada { get; private set; }
        public int UltimoLimit { get; private set; }
        public double? UltimoMinX { get; private set; }
        public double? UltimoMinY { get; private set; }
        public double? UltimoMaxX { get; private set; }
        public double? UltimoMaxY { get; private set; }

        public GeoJsonFeatureCollectionDto GeoJsonRespuesta { get; set; } = new()
        {
            Features =
            [
                new GeoJsonFeatureDto
                {
                    Id = 1,
                    Geometry = new { type = "Polygon", coordinates = new object[0] },
                    Properties = new Dictionary<string, object?> { ["UV"] = "001", ["Manzana"] = "002" }
                }
            ]
        };

        public double[]? ExtensionRespuesta { get; set; } = [-68.20, -16.55, -68.10, -16.45];

        public Task<GeoJsonFeatureCollectionDto> GetLayerGeoJsonAsync(
            string layerName,
            double? minX = null,
            double? minY = null,
            double? maxX = null,
            double? maxY = null,
            int limit = 10000,
            CancellationToken cancellationToken = default)
        {
            UltimaCapaConsultada = layerName;
            UltimoMinX = minX;
            UltimoMinY = minY;
            UltimoMaxX = maxX;
            UltimoMaxY = maxY;
            UltimoLimit = limit;
            return Task.FromResult(GeoJsonRespuesta);
        }

        public Task<IReadOnlyList<IDictionary<string, object?>>> GetLayersCatalogAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<IDictionary<string, object?>> catalogo =
            [
                new Dictionary<string, object?> { ["Capa"] = "Manzanas", ["Cantidad"] = 120 },
                new Dictionary<string, object?> { ["Capa"] = "Lotes", ["Cantidad"] = 850 },
                new Dictionary<string, object?> { ["Capa"] = "CodigosFijos", ["Cantidad"] = 430 },
                new Dictionary<string, object?> { ["Capa"] = "Vias", ["Cantidad"] = 95 }
            ];
            return Task.FromResult(catalogo);
        }

        public IReadOnlyList<GeoJsonFeatureDto> IdentificarRespuesta { get; set; } =
        [
            new GeoJsonFeatureDto
            {
                Id = 101,
                Geometry = new { type = "Polygon", coordinates = new object[0] },
                Properties = new Dictionary<string, object?> { ["_Capa"] = "Manzanas", ["Codigo"] = "MZ-01" }
            }
        ];
        public double? UltimoLngIdentificar { get; private set; }
        public double? UltimoLatIdentificar { get; private set; }
        public double? UltimaToleranciaIdentificar { get; private set; }

        public Task<IReadOnlyList<GeoJsonFeatureDto>> IdentifyAsync(double longitude, double latitude, double toleranceMeters = 10, CancellationToken cancellationToken = default)
        {
            UltimoLngIdentificar = longitude;
            UltimoLatIdentificar = latitude;
            UltimaToleranciaIdentificar = toleranceMeters;
            return Task.FromResult(IdentificarRespuesta);
        }

        public Task<double[]?> GetLayerExtentAsync(string? layerName = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExtensionRespuesta);
        }

        public Task<PagedResult<IDictionary<string, object?>>> SearchAsync(string query, string? layer = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
        {
            var resultado = new PagedResult<IDictionary<string, object?>>
            {
                Pagina = page,
                Limite = pageSize,
                TotalRegistros = 0,
                Datos = Array.Empty<IDictionary<string, object?>>()
            };
            return Task.FromResult(resultado);
        }
    }

    private sealed class BitacoraServiceFalso : IBitacoraService
    {
        public List<BitacoraEntryDto> EntradasRegistradas { get; } = [];
        public bool DebeFallar { get; set; }

        public Task<bool> RegistrarAsync(BitacoraEntryDto entrada, CancellationToken cancellationToken = default)
        {
            if (DebeFallar)
            {
                throw new InvalidOperationException("Error simulado en base de datos de bitácora.");
            }
            EntradasRegistradas.Add(entrada);
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<BitacoraItemDto>> ObtenerHistorialAsync(int limite = 100, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<BitacoraItemDto> resultado = Array.Empty<BitacoraItemDto>();
            return Task.FromResult(resultado);
        }

        public Task<IReadOnlyList<BitacoraItemDto>> ObtenerHistorialAsync(string? modulo, string? moduloExcluido, int limite = 100, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<BitacoraItemDto> resultado = Array.Empty<BitacoraItemDto>();
            return Task.FromResult(resultado);
        }
    }

    private static CapasController CrearControlador(
        CadastreRepositoryFalso cadastreRepo,
        BitacoraServiceFalso bitacoraService,
        int idUsuario = 42,
        string rol = "Consultor")
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, idUsuario.ToString()),
            new("rol", rol)
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var user = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext
        {
            User = user
        };
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        var controller = new CapasController(
            cadastreRepo,
            bitacoraService,
            NullLogger<CapasController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return controller;
    }

    [Fact]
    public void CapasController_TieneAtributoAuthorizeConRolesAdministradorYConsultor()
    {
        var atributo = typeof(CapasController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(atributo);
        Assert.Equal("Administrador,Consultor", atributo.Roles);
    }

    [Fact]
    public async Task ObtenerGeoJson_DebeRetornarFeatureCollection_YRegistrarAuditoriaEnBitacora()
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso();
        var controller = CrearControlador(repo, bitacora, idUsuario: 7, rol: "Administrador");

        var resultado = await controller.ObtenerGeoJson(
            capa: "Manzanas",
            minX: -68.20,
            minY: -16.55,
            maxX: -68.10,
            maxY: -16.45,
            limit: 500);

        var okResult = Assert.IsType<OkObjectResult>(resultado);
        var coleccion = Assert.IsType<GeoJsonFeatureCollectionDto>(okResult.Value);
        Assert.Single(coleccion.Features);

        Assert.Equal("Manzanas", repo.UltimaCapaConsultada);
        Assert.Equal(500, repo.UltimoLimit);
        Assert.Equal(-68.20, repo.UltimoMinX);

        // Verificar registro de auditoría en Bitácora según CU09
        Assert.Single(bitacora.EntradasRegistradas);
        var entrada = bitacora.EntradasRegistradas[0];
        Assert.Equal(ModulosSistema.VisorCartografico, entrada.Modulo);
        Assert.Equal("Gestionar Elementos Geográfico", entrada.Accion);
        Assert.Equal("Manzanas", entrada.Entidad);
        Assert.Equal("EXITO", entrada.Resultado);
        Assert.Equal(7, entrada.IdUsuario);
        Assert.Equal("192.168.1.100", entrada.IP);
    }

    [Fact]
    public async Task ObtenerGeoJson_LimitaParametroLimit_AlRangoPermitido()
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso();
        var controller = CrearControlador(repo, bitacora);

        // Limit excesivo (> 10000) debe acotarse a 10000
        await controller.ObtenerGeoJson("Lotes", limit: 99999);
        Assert.Equal(10000, repo.UltimoLimit);

        // Limit menor o igual a 0 debe acotarse al valor por defecto (2000)
        await controller.ObtenerGeoJson("Lotes", limit: -5);
        Assert.Equal(2000, repo.UltimoLimit);
    }

    [Fact]
    public async Task ObtenerGeoJson_FalloEnBitacora_NoInterrumpeRespuestaGeoJson()
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso { DebeFallar = true };
        var controller = CrearControlador(repo, bitacora);

        var resultado = await controller.ObtenerGeoJson("CodigosFijos");

        var okResult = Assert.IsType<OkObjectResult>(resultado);
        var coleccion = Assert.IsType<GeoJsonFeatureCollectionDto>(okResult.Value);
        Assert.NotNull(coleccion);
    }

    [Fact]
    public async Task ObtenerCatalogoCapas_DevuelveCatalogoDeCapas()
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso();
        var controller = CrearControlador(repo, bitacora);

        var resultado = await controller.ObtenerCatalogoCapas(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(resultado);
        var lista = Assert.IsAssignableFrom<IReadOnlyList<IDictionary<string, object?>>>(okResult.Value);
        Assert.Equal(4, lista.Count);
    }

    [Fact]
    public async Task ObtenerExtension_General_DebeRetornarBbox_YRegistrarAuditoriaEnBitacora()
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso();
        var controller = CrearControlador(repo, bitacora, idUsuario: 22, rol: "Administrador");

        var resultado = await controller.ObtenerExtension(capa: null);

        var okResult = Assert.IsType<OkObjectResult>(resultado);
        Assert.NotNull(okResult.Value);

        // Registro de auditoría según CU11 - Gestionar Extensión
        Assert.Single(bitacora.EntradasRegistradas);
        var entrada = bitacora.EntradasRegistradas[0];
        Assert.Equal(ModulosSistema.VisorCartografico, entrada.Modulo);
        Assert.Equal("Gestionar Extensión", entrada.Accion);
        Assert.Equal("General", entrada.Entidad);
        Assert.Equal("EXITO", entrada.Resultado);
        Assert.Equal(22, entrada.IdUsuario);
        Assert.Equal("192.168.1.100", entrada.IP);
    }

    [Fact]
    public async Task ObtenerExtension_PorCapa_DebeRetornarBbox_YRegistrarAuditoriaEnBitacora()
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso();
        var controller = CrearControlador(repo, bitacora, idUsuario: 33, rol: "Consultor");

        var resultado = await controller.ObtenerExtension(capa: "Manzanas");

        var okResult = Assert.IsType<OkObjectResult>(resultado);
        Assert.NotNull(okResult.Value);

        // Registro de auditoría según CU11 - Gestionar Extensión
        Assert.Single(bitacora.EntradasRegistradas);
        var entrada = bitacora.EntradasRegistradas[0];
        Assert.Equal(ModulosSistema.VisorCartografico, entrada.Modulo);
        Assert.Equal("Gestionar Extensión", entrada.Accion);
        Assert.Equal("Manzanas", entrada.Entidad);
        Assert.Equal("EXITO", entrada.Resultado);
        Assert.Equal(33, entrada.IdUsuario);
    }

    [Fact]
    public async Task ObtenerExtension_FalloEnBitacora_NoInterrumpeRespuesta()
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso { DebeFallar = true };
        var controller = CrearControlador(repo, bitacora);

        var resultado = await controller.ObtenerExtension(capa: "Vias");

        var okResult = Assert.IsType<OkObjectResult>(resultado);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task ObtenerExtension_CuandoEsNull_DevuelveNotFound()
    {
        var repo = new CadastreRepositoryFalso { ExtensionRespuesta = null };
        var bitacora = new BitacoraServiceFalso();
        var controller = CrearControlador(repo, bitacora);

        var resultado = await controller.ObtenerExtension(capa: "Inexistente");

        Assert.IsType<NotFoundObjectResult>(resultado);
    }

    [Fact]
    public async Task Identificar_DebeRetornarElementos_YRegistrarAuditoriaEnBitacora()
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso();
        var controller = CrearControlador(repo, bitacora, idUsuario: 15, rol: "Consultor");

        var resultado = await controller.Identificar(lng: -68.123456, lat: -16.543210, tolerancia: 12.5);

        var okResult = Assert.IsType<OkObjectResult>(resultado);
        var elementos = Assert.IsAssignableFrom<IReadOnlyList<GeoJsonFeatureDto>>(okResult.Value);
        Assert.Single(elementos);
        Assert.Equal(101, elementos[0].Id);

        Assert.Equal(-68.123456, repo.UltimoLngIdentificar);
        Assert.Equal(-16.543210, repo.UltimoLatIdentificar);
        Assert.Equal(12.5, repo.UltimaToleranciaIdentificar);

        // Registro de auditoría según CU10 - Gestionar Entidades
        Assert.Single(bitacora.EntradasRegistradas);
        var entrada = bitacora.EntradasRegistradas[0];
        Assert.Equal(ModulosSistema.VisorCartografico, entrada.Modulo);
        Assert.Equal("Gestionar Entidades", entrada.Accion);
        Assert.Equal("Identificación", entrada.Entidad);
        Assert.Equal("EXITO", entrada.Resultado);
        Assert.Equal(15, entrada.IdUsuario);
        Assert.Equal("192.168.1.100", entrada.IP);
    }

    [Fact]
    public async Task Identificar_FalloEnBitacora_NoInterrumpeRespuesta()
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso { DebeFallar = true };
        var controller = CrearControlador(repo, bitacora);

        var resultado = await controller.Identificar(lng: -68.15, lat: -16.50);

        var okResult = Assert.IsType<OkObjectResult>(resultado);
        var elementos = Assert.IsAssignableFrom<IReadOnlyList<GeoJsonFeatureDto>>(okResult.Value);
        Assert.Single(elementos);
    }
}
