using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite.Geometries;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.DTOs.CodigosFijos;
using VisorDatosSIG.Application.DTOs.Manzanas;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Spatial;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas del backend de CU14 - Consultar Manzana.
/// </summary>
public sealed class ManzanasControllerTests
{
    private sealed class CadastreRepositoryFalso : ICadastreRepository
    {
        public ManzanaConsultaDto? ConsultaRecibida { get; private set; }
        public int? IdRecibido { get; private set; }
        public int BusquedasRealizadas { get; private set; }
        public int DetallesConsultados { get; private set; }

        public PagedResult<ManzanaResumenDto> PaginaRespuesta { get; set; } = new()
        {
            Pagina = 1,
            Limite = 20,
            TotalRegistros = 1,
            Datos =
            [
                new ManzanaResumenDto
                {
                    IdManzana = 76,
                    UvMza = "M-1",
                    Uv = "30",
                    Mza = "1"
                }
            ]
        };

        public ManzanaDetalleDto? DetalleRespuesta { get; set; }

        public Task<PagedResult<ManzanaResumenDto>> SearchManzanasAsync(
            ManzanaConsultaDto consulta,
            CancellationToken cancellationToken = default)
        {
            BusquedasRealizadas++;
            ConsultaRecibida = consulta;
            return Task.FromResult(PaginaRespuesta with
            {
                Pagina = consulta.Pagina,
                Limite = consulta.Limite
            });
        }

        public Task<ManzanaDetalleDto?> GetManzanaByIdAsync(
            int idManzana,
            CancellationToken cancellationToken = default)
        {
            DetallesConsultados++;
            IdRecibido = idManzana;
            return Task.FromResult(DetalleRespuesta);
        }

        public Task<PagedResult<CodigoFijoResumenDto>> SearchCodigosFijosAsync(
            CodigoFijoConsultaDto consulta,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CodigoFijoDetalleDto?> GetCodigoFijoByIdAsync(
            int idCodigo,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GeoJsonFeatureCollectionDto> GetLayerGeoJsonAsync(
            string layerName,
            double? minX = null,
            double? minY = null,
            double? maxX = null,
            double? maxY = null,
            int limit = 2000,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<GeoJsonFeatureDto>> IdentifyAsync(
            double longitude,
            double latitude,
            double toleranceMeters = 10,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PagedResult<IDictionary<string, object?>>> SearchAsync(
            string query,
            string? layer = null,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IDictionary<string, object?>>> GetLayersCatalogAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<double[]?> GetLayerExtentAsync(
            string? layerName = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class PermisoServiceFalso(bool puedeVer) : IPermisoService
    {
        public string? UrlConsultada { get; private set; }
        public AccionPermiso? AccionConsultada { get; private set; }

        public Task<PermisosEfectivosDto> ObtenerPermisosAsync(
            int idUsuario,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PermisosEfectivosDto { TieneAccesoWeb = puedeVer });

        public Task<bool> TieneAccesoWebAsync(
            int idUsuario,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(puedeVer);

        public Task<bool> TienePermisoAsync(
            int idUsuario,
            string menuUrl,
            AccionPermiso accion,
            CancellationToken cancellationToken = default)
        {
            UrlConsultada = menuUrl;
            AccionConsultada = accion;
            return Task.FromResult(puedeVer);
        }

        public Task<bool> PuedeVerAsync(
            int idUsuario,
            string menuUrl,
            CancellationToken cancellationToken = default)
        {
            UrlConsultada = menuUrl;
            AccionConsultada = AccionPermiso.Ver;
            return Task.FromResult(puedeVer);
        }
    }

    private sealed class BitacoraServiceFalso : IBitacoraService
    {
        public List<BitacoraEntryDto> Entradas { get; } = [];
        public bool RetornaExito { get; set; } = true;
        public bool LanzaExcepcion { get; set; }

        public Task<bool> RegistrarAsync(
            BitacoraEntryDto entrada,
            CancellationToken cancellationToken = default)
        {
            if (LanzaExcepcion)
            {
                throw new InvalidOperationException("Fallo simulado de bitacora.");
            }

            Entradas.Add(entrada);
            return Task.FromResult(RetornaExito);
        }

        public Task<IReadOnlyList<BitacoraItemDto>> ObtenerHistorialAsync(
            int limite = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BitacoraItemDto>>([]);

        public Task<IReadOnlyList<BitacoraItemDto>> ObtenerHistorialAsync(
            string? modulo,
            string? moduloExcluido,
            int limite = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BitacoraItemDto>>([]);
    }

    private static ManzanasController CrearControlador(
        CadastreRepositoryFalso repository,
        bool puedeVer = true,
        int? idUsuario = 42,
        BitacoraServiceFalso? bitacora = null,
        PermisoServiceFalso? permiso = null)
    {
        var claims = idUsuario.HasValue
            ? new[]
            {
                new Claim(
                    JwtRegisteredClaimNames.Sub,
                    idUsuario.Value.ToString(CultureInfo.InvariantCulture))
            }
            : [];

        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
        };
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.10.7");

        return new ManzanasController(
            repository,
            permiso ?? new PermisoServiceFalso(puedeVer),
            bitacora ?? new BitacoraServiceFalso(),
            NullLogger<ManzanasController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static Polygon CrearPoligono(GeometryFactory factory, double x, double y) =>
        factory.CreatePolygon(
        [
            new Coordinate(x, y),
            new Coordinate(x + 0.01, y),
            new Coordinate(x + 0.01, y + 0.01),
            new Coordinate(x, y + 0.01),
            new Coordinate(x, y)
        ]);

    [Fact(DisplayName = "CU14: expone rutas y exige autenticacion sin roles fijos")]
    public void ExponeContratoHttpSeguro()
    {
        var tipo = typeof(ManzanasController);
        var authorize = tipo.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Null(authorize!.Roles);
        Assert.Equal("api/manzanas", tipo.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Null(tipo.GetMethod(nameof(ManzanasController.Buscar))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("{idManzana:int}", tipo.GetMethod(nameof(ManzanasController.Obtener))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    [Fact(DisplayName = "CU14: usuario autorizado obtiene pagina sin filtros y sin N+1")]
    public async Task BuscarSinFiltrosDevuelvePaginaSinNMasUno()
    {
        var repository = new CadastreRepositoryFalso();

        var resultado = await CrearControlador(repository).Buscar(cancellationToken: CancellationToken.None);

        var pagina = Assert.IsType<PagedResult<ManzanaResumenDto>>(
            Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(1, pagina.Pagina);
        Assert.Equal(20, pagina.Limite);
        Assert.Equal("M-1", Assert.Single(pagina.Datos).UvMza);
        Assert.Null(repository.ConsultaRecibida!.UvMza);
        Assert.Null(repository.ConsultaRecibida.Uv);
        Assert.Null(repository.ConsultaRecibida.Mza);
        Assert.Equal(1, repository.BusquedasRealizadas);
        Assert.Equal(0, repository.DetallesConsultados);
    }

    [Theory(DisplayName = "CU14: transmite filtros individuales recortando espacios")]
    [InlineData("  M-12  ", null, null, "M-12", null, null)]
    [InlineData(null, "  30  ", null, null, "30", null)]
    [InlineData(null, null, "  5  ", null, null, "5")]
    public async Task BuscarTransmiteFiltros(
        string? uvMza,
        string? uv,
        string? mza,
        string? esperadoUvMza,
        string? esperadoUv,
        string? esperadoMza)
    {
        var repository = new CadastreRepositoryFalso();

        var resultado = await CrearControlador(repository).Buscar(
            uvMza,
            uv,
            mza,
            cancellationToken: CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(esperadoUvMza, repository.ConsultaRecibida!.UvMza);
        Assert.Equal(esperadoUv, repository.ConsultaRecibida.Uv);
        Assert.Equal(esperadoMza, repository.ConsultaRecibida.Mza);
    }

    [Theory(DisplayName = "CU14: combina filtros y conserva paginacion valida")]
    [InlineData(null, "30", "5", 2, 8)]
    [InlineData("M-5", "30", "5", 3, 100)]
    public async Task BuscarCombinaFiltrosYPaginacion(
        string? uvMza,
        string uv,
        string mza,
        int pagina,
        int limite)
    {
        var repository = new CadastreRepositoryFalso();

        var resultado = await CrearControlador(repository).Buscar(
            uvMza,
            uv,
            mza,
            pagina,
            limite,
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        var consulta = Assert.IsType<ManzanaConsultaDto>(repository.ConsultaRecibida);
        Assert.Equal((uvMza, uv, mza, pagina, limite),
            (consulta.UvMza, consulta.Uv, consulta.Mza, consulta.Pagina, consulta.Limite));
    }

    [Theory(DisplayName = "CU14: rechaza pagina y limite fuera de rango")]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task BuscarRechazaPaginacionInvalida(int pagina, int limite)
    {
        var repository = new CadastreRepositoryFalso();

        var resultado = await CrearControlador(repository).Buscar(
            pagina: pagina,
            limite: limite,
            cancellationToken: CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(resultado).StatusCode);
        Assert.Equal(0, repository.BusquedasRealizadas);
    }

    [Fact(DisplayName = "CU14: usuario sin permiso recibe 403 antes de consultar datos")]
    public async Task SinPermisoDevuelveForbidden()
    {
        var repository = new CadastreRepositoryFalso();
        var permiso = new PermisoServiceFalso(puedeVer: false);

        var resultado = await CrearControlador(repository, permiso: permiso)
            .Buscar(cancellationToken: CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(resultado).StatusCode);
        Assert.Equal(PermisoMenu.ConsultaManzana, permiso.UrlConsultada);
        Assert.Equal(AccionPermiso.Ver, permiso.AccionConsultada);
        Assert.Equal(0, repository.BusquedasRealizadas);
    }

    [Fact(DisplayName = "CU14: usuario sin identidad recibe 401 antes de consultar datos")]
    public async Task SinIdentidadDevuelveUnauthorized()
    {
        var repository = new CadastreRepositoryFalso();

        var resultado = await CrearControlador(repository, idUsuario: null)
            .Buscar(cancellationToken: CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(resultado);
        Assert.Equal(0, repository.BusquedasRealizadas);
    }

    [Fact(DisplayName = "CU14: detalle existente se consulta exclusivamente por IdManzana")]
    public async Task DetalleExistenteDevuelveOk()
    {
        var repository = new CadastreRepositoryFalso
        {
            DetalleRespuesta = new ManzanaDetalleDto
            {
                IdManzana = 76,
                IdOrigen = 500,
                UvMza = "M-1",
                Uv = "30",
                Mza = "1"
            }
        };

        var detalle = Assert.IsType<ManzanaDetalleDto>(
            Assert.IsType<OkObjectResult>(
                await CrearControlador(repository).Obtener(76, CancellationToken.None)).Value);

        Assert.Equal(76, detalle.IdManzana);
        Assert.Equal(76, repository.IdRecibido);
    }

    [Fact(DisplayName = "CU14: detalle inexistente devuelve 404")]
    public async Task DetalleInexistenteDevuelveNotFound()
    {
        var resultado = await CrearControlador(new CadastreRepositoryFalso())
            .Obtener(999, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(resultado).StatusCode);
    }

    [Fact(DisplayName = "CU14: IdOrigen campos alfanumericos y Geom nulos se serializan")]
    public async Task DetalleConNulosSeSerializa()
    {
        var repository = new CadastreRepositoryFalso
        {
            DetalleRespuesta = new ManzanaDetalleDto { IdManzana = 7 }
        };

        var detalle = Assert.IsType<ManzanaDetalleDto>(
            Assert.IsType<OkObjectResult>(
                await CrearControlador(repository).Obtener(7, CancellationToken.None)).Value);

        Assert.Null(detalle.IdOrigen);
        Assert.Null(detalle.UvMza);
        Assert.Null(detalle.Uv);
        Assert.Null(detalle.Mza);
        Assert.Null(detalle.Geometria);

        var json = JsonSerializer.Serialize(detalle, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        Assert.Contains("\"idOrigen\":null", json);
        Assert.Contains("\"uvMza\":null", json);
        Assert.Contains("\"geometria\":null", json);
    }

    [Fact(DisplayName = "CU14: Polygon conserva GeoJSON y orden longitud latitud")]
    public async Task DetallePolygonConservaGeometriaYOrden()
    {
        const double longitud = -63.1823;
        const double latitud = -17.7834;
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var repository = new CadastreRepositoryFalso
        {
            DetalleRespuesta = new ManzanaDetalleDto
            {
                IdManzana = 10,
                Geometria = SpatialGeoJsonHelper.ToGeoJsonObject(
                    CrearPoligono(factory, longitud, latitud))
            }
        };

        var detalle = Assert.IsType<ManzanaDetalleDto>(
            Assert.IsType<OkObjectResult>(
                await CrearControlador(repository).Obtener(10, CancellationToken.None)).Value);
        var json = JsonSerializer.SerializeToElement(detalle.Geometria);

        Assert.Equal("Polygon", json.GetProperty("type").GetString());
        var primeraCoordenada = json.GetProperty("coordinates")[0][0];
        Assert.Equal(longitud, primeraCoordenada[0].GetDouble());
        Assert.Equal(latitud, primeraCoordenada[1].GetDouble());
    }

    [Fact(DisplayName = "CU14: MultiPolygon se conserva sin convertir a Polygon o Point")]
    public async Task DetalleMultiPolygonConservaTipo()
    {
        var factory = new GeometryFactory(new PrecisionModel(), 4326);
        var multipoligono = factory.CreateMultiPolygon(
        [
            CrearPoligono(factory, -63.18, -17.78),
            CrearPoligono(factory, -63.16, -17.76)
        ]);
        var repository = new CadastreRepositoryFalso
        {
            DetalleRespuesta = new ManzanaDetalleDto
            {
                IdManzana = 11,
                Geometria = SpatialGeoJsonHelper.ToGeoJsonObject(multipoligono)
            }
        };

        var detalle = Assert.IsType<ManzanaDetalleDto>(
            Assert.IsType<OkObjectResult>(
                await CrearControlador(repository).Obtener(11, CancellationToken.None)).Value);
        var json = JsonSerializer.SerializeToElement(detalle.Geometria);

        Assert.Equal("MultiPolygon", json.GetProperty("type").GetString());
        Assert.Equal(2, json.GetProperty("coordinates").GetArrayLength());
    }

    [Theory(DisplayName = "CU14: fallo de bitacora no bloquea respuesta")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FalloDeBitacoraNoBloquea(bool lanzarExcepcion)
    {
        var bitacora = new BitacoraServiceFalso
        {
            RetornaExito = false,
            LanzaExcepcion = lanzarExcepcion
        };

        var resultado = await CrearControlador(
                new CadastreRepositoryFalso(),
                bitacora: bitacora)
            .Buscar(cancellationToken: CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
    }

    [Fact(DisplayName = "CU14: registra busqueda y detalle una vez por operacion")]
    public async Task RegistraBitacoraDeBusquedaYDetalle()
    {
        var bitacora = new BitacoraServiceFalso();
        var repository = new CadastreRepositoryFalso
        {
            PaginaRespuesta = new PagedResult<ManzanaResumenDto>
            {
                TotalRegistros = 27,
                Datos = [new ManzanaResumenDto { IdManzana = 76 }]
            },
            DetalleRespuesta = new ManzanaDetalleDto { IdManzana = 76 }
        };
        var controller = CrearControlador(repository, bitacora: bitacora);

        await controller.Buscar(cancellationToken: CancellationToken.None);
        await controller.Obtener(76, CancellationToken.None);

        Assert.Equal(2, bitacora.Entradas.Count);
        Assert.All(bitacora.Entradas, entrada =>
        {
            Assert.Equal(ModulosSistema.ConsultasYFiltros, entrada.Modulo);
            Assert.Equal("Consultar Manzana", entrada.Accion);
            Assert.Equal("Manzanas", entrada.Entidad);
        });
        Assert.Contains("coincidencias: 27", bitacora.Entradas[0].Detalle);
        Assert.Null(bitacora.Entradas[0].IdEntidad);
        Assert.Equal(76, bitacora.Entradas[1].IdEntidad);
    }

    [Fact(DisplayName = "CU14: endpoint generico api/catastro/buscar permanece disponible")]
    public void BusquedaGenericaPermaneceIntacta()
    {
        var metodo = typeof(CatastroController).GetMethod(nameof(CatastroController.Buscar));

        Assert.NotNull(metodo);
        Assert.Equal("buscar", metodo!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }
}
