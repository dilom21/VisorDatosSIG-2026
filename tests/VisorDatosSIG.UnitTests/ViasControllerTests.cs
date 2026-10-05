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

using VisorDatosSIG.Application.DTOs.Lotes;
using VisorDatosSIG.Application.DTOs.Vias;

namespace VisorDatosSIG.UnitTests;

public sealed class ViasControllerTests
{
    private sealed class CadastreRepositoryFalso : ICadastreRepository
    {
    public Task<PagedResult<VisorDatosSIG.Application.DTOs.Lotes.LoteResumenDto>> SearchLotesAsync(VisorDatosSIG.Application.DTOs.Lotes.LoteConsultaDto consulta, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<VisorDatosSIG.Application.DTOs.Lotes.LoteDetalleDto?> GetLoteByIdAsync(int idLote, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ViaConsultaDto? Filtros { get; private set; }
        public ViaDetalleDto? Detalle { get; set; }
        public bool SinResultados { get; set; }
        public int Consultas { get; private set; }
        public Task<PagedResult<ViaResumenDto>> SearchViasAsync(ViaConsultaDto consulta, CancellationToken cancellationToken = default)
        {
            Consultas++; Filtros = consulta;
            return Task.FromResult(new PagedResult<ViaResumenDto> { Pagina=consulta.Pagina, Limite=consulta.Limite, TotalRegistros=SinResultados ? 0 : 1, Datos=SinResultados ? [] : [new ViaResumenDto { IdVia = 7 }] });
        }
        public Task<ViaDetalleDto?> GetViaByIdAsync(int idVia, CancellationToken cancellationToken = default) { Consultas++; return Task.FromResult(Detalle); }


        public Task<PagedResult<ManzanaResumenDto>> SearchManzanasAsync(ManzanaConsultaDto consulta, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ManzanaDetalleDto?> GetManzanaByIdAsync(int idManzana, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<CodigoFijoResumenDto>> SearchCodigosFijosAsync(CodigoFijoConsultaDto consulta, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CodigoFijoDetalleDto?> GetCodigoFijoByIdAsync(int idCodigo, CancellationToken cancellationToken = default) => throw new NotSupportedException();

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


    private static ViasController Crear(CadastreRepositoryFalso repo, bool acceso = true,
        int? usuario = 42, PermisoServiceFalso? permiso = null, BitacoraServiceFalso? bitacora = null)
    {
        var claims = usuario.HasValue ? new[] { new Claim("sub", usuario.Value.ToString()) } : [];
        return new ViasController(repo, permiso ?? new PermisoServiceFalso(acceso),
            bitacora ?? new BitacoraServiceFalso(), NullLogger<ViasController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) } }
        };
    }

    [Fact]
    public void ExigeAutenticacionYExponeRutas()
    {
        var tipo = typeof(ViasController);
        Assert.NotNull(tipo.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Null(tipo.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("api/vias", tipo.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("{idVia:int}", tipo.GetMethod("Obtener")!.GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeniegaAntesDeConsultarDatos(bool detalle)
    {
        var repo = new CadastreRepositoryFalso();
        var permiso = new PermisoServiceFalso(false);
        var ctrl = Crear(repo, permiso:permiso);
        var r = detalle ? await ctrl.Obtener(7) : await ctrl.Buscar();
        Assert.Equal(403, Assert.IsType<ObjectResult>(r).StatusCode);
        Assert.Equal(PermisoMenu.ConsultaVias, permiso.UrlConsultada);
        Assert.Equal(0,repo.Consultas);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RechazaIdentidadAusente(bool detalle)
    {
        var repo = new CadastreRepositoryFalso();
        var ctrl = Crear(repo, usuario:null);
        Assert.IsType<UnauthorizedResult>(detalle ? await ctrl.Obtener(7) : await ctrl.Buscar());
        Assert.Equal(0,repo.Consultas);
    }

    [Theory]
    [InlineData(0,20)]
    [InlineData(-1,20)]
    [InlineData(1,0)]
    [InlineData(1,101)]
    public async Task ValidaPaginacion(int pagina, int limite)
    {
        var repo = new CadastreRepositoryFalso();
        Assert.Equal(400,Assert.IsType<ObjectResult>(await Crear(repo).Buscar(pagina:pagina,limite:limite)).StatusCode);
        Assert.Equal(0,repo.Consultas);
    }

    [Fact]
    public async Task ConservaPaginaYRegistraBitacora()
    {
        var repo = new CadastreRepositoryFalso();
        var auditoria = new BitacoraServiceFalso();
        var r = await Crear(repo,bitacora:auditoria).Buscar(pagina:3,limite:10);
        var datos=Assert.IsType<PagedResult<ViaResumenDto>>(Assert.IsType<OkObjectResult>(r).Value);
        Assert.Equal(3,datos.Pagina); Assert.Equal(10,datos.Limite);
        Assert.Single(datos.Datos); Assert.Single(auditoria.Entradas);
        Assert.Equal("Vias",auditoria.Entradas[0].Entidad);
        Assert.Equal(1,repo.Consultas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task RechazaIdentificadorInvalido(int id)
    {
        var repo = new CadastreRepositoryFalso();
        Assert.Equal(400, Assert.IsType<ObjectResult>(await Crear(repo).Obtener(id)).StatusCode);
        Assert.Equal(0,repo.Consultas);
    }

    [Fact]
    public async Task Devuelve404SiNoExiste()
    {
        Assert.Equal(404, Assert.IsType<ObjectResult>(await Crear(new CadastreRepositoryFalso()).Obtener(7)).StatusCode);
    }

    [Fact]
    public async Task DetalleSinGeometriaEsValido()
    {
        var repo=new CadastreRepositoryFalso { Detalle=new ViaDetalleDto { IdVia=7 } };
        var dto=Assert.IsType<ViaDetalleDto>(Assert.IsType<OkObjectResult>(await Crear(repo).Obtener(7)).Value);
        Assert.Null(dto.Geometria);
        Assert.Contains("\"geometria\":null",JsonSerializer.Serialize(dto,new JsonSerializerOptions { PropertyNamingPolicy=JsonNamingPolicy.CamelCase,DefaultIgnoreCondition=JsonIgnoreCondition.WhenWritingNull }));
    }

    [Fact]
    public async Task FalloDeBitacoraNoBloquea()
    {
        var repo=new CadastreRepositoryFalso { Detalle=new ViaDetalleDto { IdVia=7 } };
        var ctrl=Crear(repo,bitacora:new BitacoraServiceFalso { LanzaExcepcion=true });
        Assert.IsType<OkObjectResult>(await ctrl.Buscar());
        Assert.IsType<OkObjectResult>(await ctrl.Obtener(7));
    }

    [Fact]
    public async Task CombinaNombreTipoYOSMID()
    {
        var repo=new CadastreRepositoryFalso();
        await Crear(repo).Buscar(nombre:"  Avenida  ",tipoVia:"  Principal  ",osmid:"  123  ");
        Assert.Equal("Avenida",repo.Filtros!.Nombre);
        Assert.Equal("Principal",repo.Filtros.TipoVia);
        Assert.Equal("123",repo.Filtros.Osmid);
    }
    [Fact]
    public async Task FiltrosVaciosSonNulos()
    {
        var repo=new CadastreRepositoryFalso();
        await Crear(repo).Buscar(nombre:" ",tipoVia:" ",osmid:" ");
        Assert.Null(repo.Filtros!.Nombre); Assert.Null(repo.Filtros.TipoVia); Assert.Null(repo.Filtros.Osmid);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConservaLineasYMultilineas(bool multiple)
    {
        var factory=new GeometryFactory(new PrecisionModel(),4326);
        var line=factory.CreateLineString([new Coordinate(-63,-17),new Coordinate(-62.9,-16.9)]);
        Geometry geom=multiple ? factory.CreateMultiLineString([line]) : line;
        var objeto=SpatialGeoJsonHelper.ToGeoJsonObject(geom);
        var repo=new CadastreRepositoryFalso { Detalle=new ViaDetalleDto { IdVia=7,Geometria=objeto } };
        var dto=Assert.IsType<ViaDetalleDto>(Assert.IsType<OkObjectResult>(await Crear(repo).Obtener(7)).Value);
        Assert.Same(objeto,dto.Geometria);
        var json=JsonSerializer.Serialize(dto.Geometria);
        Assert.Contains(multiple ? "MultiLineString" : "LineString",json);
        Assert.Contains("-63",json); Assert.Contains("-17",json);
    }

    [Fact]
    public async Task SinCoincidenciasDevuelvePaginaVacia()
    {
        var repo = new CadastreRepositoryFalso { SinResultados=true };
        var dto=Assert.IsType<PagedResult<ViaResumenDto>>(Assert.IsType<OkObjectResult>(await Crear(repo).Buscar()).Value);
        Assert.Empty(dto.Datos); Assert.Equal(0,dto.TotalRegistros); Assert.Equal(0,dto.TotalPaginas);
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Supervisor")]
    [InlineData("Empleado")]
    [InlineData("Consultor")]
    public async Task RolesWebConsultanConPermiso(string rol)
    {
        var ctrl=Crear(new CadastreRepositoryFalso());
        ((ClaimsIdentity)ctrl.User.Identity!).AddClaim(new Claim(ClaimTypes.Role,rol));
        Assert.IsType<OkObjectResult>(await ctrl.Buscar());
    }
}
