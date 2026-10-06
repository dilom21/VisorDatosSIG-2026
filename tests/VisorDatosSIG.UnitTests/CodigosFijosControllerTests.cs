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
/// Pruebas del backend de CU13 - Consultar Codigo Fijo.
/// </summary>
public sealed class CodigosFijosControllerTests
{
    private sealed class CadastreRepositoryFalso : ICadastreRepository
    {
        public CodigoFijoConsultaDto? ConsultaRecibida { get; private set; }
        public int? IdRecibido { get; private set; }
        public int ConsultasRealizadas { get; private set; }

        public PagedResult<CodigoFijoResumenDto> PaginaRespuesta { get; set; } = new()
        {
            Pagina = 1,
            Limite = 20,
            TotalRegistros = 1,
            Datos =
            [
                new CodigoFijoResumenDto
                {
                    IdCodigo = 17,
                    CodFSig = "CF-0017",
                    CodFijo = 300,
                    Nombre = "Mercado Central",
                    Estado = 1
                }
            ]
        };

        public CodigoFijoDetalleDto? DetalleRespuesta { get; set; }

        public Task<PagedResult<CodigoFijoResumenDto>> SearchCodigosFijosAsync(
            CodigoFijoConsultaDto consulta,
            CancellationToken cancellationToken = default)
        {
            ConsultasRealizadas++;
            ConsultaRecibida = consulta;
            return Task.FromResult(PaginaRespuesta with
            {
                Pagina = consulta.Pagina,
                Limite = consulta.Limite
            });
        }

        public Task<CodigoFijoDetalleDto?> GetCodigoFijoByIdAsync(
            int idCodigo,
            CancellationToken cancellationToken = default)
        {
            ConsultasRealizadas++;
            IdRecibido = idCodigo;
            return Task.FromResult(DetalleRespuesta);
        }

        public Task<PagedResult<ManzanaResumenDto>> SearchManzanasAsync(
            ManzanaConsultaDto consulta,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<ManzanaResumenDto>
            {
                Pagina = consulta.Pagina,
                Limite = consulta.Limite
            });

        public Task<ManzanaDetalleDto?> GetManzanaByIdAsync(
            int idManzana,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ManzanaDetalleDto?>(null);

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

    private static CodigosFijosController CrearControlador(
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
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.10.5");

        return new CodigosFijosController(
            repository,
            permiso ?? new PermisoServiceFalso(puedeVer),
            bitacora ?? new BitacoraServiceFalso(),
            NullLogger<CodigosFijosController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    [Fact(DisplayName = "CU13: expone las dos rutas y exige autenticacion sin roles fijos")]
    public void ExponeContratoHttpSeguro()
    {
        var tipo = typeof(CodigosFijosController);
        var authorize = tipo.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Null(authorize!.Roles);
        Assert.Equal("api/codigos-fijos", tipo.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Null(tipo.GetMethod(nameof(CodigosFijosController.Buscar))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal("{idCodigo:int}", tipo.GetMethod(nameof(CodigosFijosController.Obtener))!
            .GetCustomAttribute<HttpGetAttribute>()!.Template);
    }

    [Fact(DisplayName = "CU13: usuario autorizado obtiene 200 sin filtros y valores por defecto")]
    public async Task BuscarSinFiltrosDevuelvePagina()
    {
        var repository = new CadastreRepositoryFalso();

        var resultado = await CrearControlador(repository).Buscar(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var pagina = Assert.IsType<PagedResult<CodigoFijoResumenDto>>(ok.Value);
        Assert.Equal(1, pagina.Pagina);
        Assert.Equal(20, pagina.Limite);
        Assert.Equal("Normal", Assert.Single(pagina.Datos).EstadoDescripcion);
        Assert.Null(repository.ConsultaRecibida!.CodFSig);
        Assert.Null(repository.ConsultaRecibida.Nombre);
    }

    [Theory(DisplayName = "CU13: transmite cada filtro individual recortando textos")]
    [InlineData("  CF-77  ", null, null, null, "CF-77", null, null, null)]
    [InlineData(null, 7788, null, null, null, 7788, null, null)]
    [InlineData(null, null, "  Mercado  ", null, null, null, "Mercado", null)]
    [InlineData(null, null, null, 4, null, null, null, 4)]
    public async Task BuscarTransmiteFiltros(
        string? codFSig,
        int? codFijo,
        string? nombre,
        int? estado,
        string? esperadoCodFSig,
        int? esperadoCodFijo,
        string? esperadoNombre,
        int? esperadoEstado)
    {
        var repository = new CadastreRepositoryFalso();

        var resultado = await CrearControlador(repository).Buscar(
            codFSig,
            codFijo,
            nombre,
            estado.HasValue ? (byte?)estado.Value : null,
            cancellationToken: CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(esperadoCodFSig, repository.ConsultaRecibida!.CodFSig);
        Assert.Equal(esperadoCodFijo, repository.ConsultaRecibida.CodFijo);
        Assert.Equal(esperadoNombre, repository.ConsultaRecibida.Nombre);
        Assert.Equal(esperadoEstado.HasValue ? (byte?)esperadoEstado.Value : null, repository.ConsultaRecibida.Estado);
    }

    [Fact(DisplayName = "CU13: combina filtros y conserva la paginacion solicitada")]
    public async Task BuscarCombinaFiltrosYPaginacion()
    {
        var repository = new CadastreRepositoryFalso();

        var resultado = await CrearControlador(repository).Buscar(
            codFSig: "CF",
            codFijo: 510,
            nombre: "Escuela",
            estado: 2,
            pagina: 3,
            limite: 15,
            cancellationToken: CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        var consulta = Assert.IsType<CodigoFijoConsultaDto>(repository.ConsultaRecibida);
        Assert.Equal(("CF", 510, "Escuela", (byte?)2, 3, 15),
            (consulta.CodFSig, consulta.CodFijo, consulta.Nombre, consulta.Estado, consulta.Pagina, consulta.Limite));
    }

    [Theory(DisplayName = "CU13: rechaza paginacion y estado fuera de rango")]
    [InlineData(0, 20, null)]
    [InlineData(1, 0, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 20, 0)]
    [InlineData(1, 20, 6)]
    public async Task BuscarRechazaParametrosInvalidos(int pagina, int limite, int? estado)
    {
        var repository = new CadastreRepositoryFalso();

        var resultado = await CrearControlador(repository).Buscar(
            estado: estado.HasValue ? (byte?)estado.Value : null,
            pagina: pagina,
            limite: limite,
            cancellationToken: CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsType<ObjectResult>(resultado).StatusCode);
        Assert.Equal(0, repository.ConsultasRealizadas);
    }

    [Fact(DisplayName = "CU13: sin permiso devuelve 403 antes de consultar datos")]
    public async Task SinPermisoDevuelveForbidden()
    {
        var repository = new CadastreRepositoryFalso();
        var permiso = new PermisoServiceFalso(puedeVer: false);

        var resultado = await CrearControlador(repository, permiso: permiso)
            .Buscar(cancellationToken: CancellationToken.None);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(resultado).StatusCode);
        Assert.Equal(PermisoMenu.ConsultaCodigoFijo, permiso.UrlConsultada);
        Assert.Equal(AccionPermiso.Ver, permiso.AccionConsultada);
        Assert.Equal(0, repository.ConsultasRealizadas);
    }

    [Fact(DisplayName = "CU13: sin identidad devuelve 401 antes de consultar datos")]
    public async Task SinIdentidadDevuelveUnauthorized()
    {
        var repository = new CadastreRepositoryFalso();

        var resultado = await CrearControlador(repository, idUsuario: null)
            .Buscar(cancellationToken: CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(resultado);
        Assert.Equal(0, repository.ConsultasRealizadas);
    }

    [Fact(DisplayName = "CU13: detalle existente devuelve Point GeoJSON en orden longitud latitud")]
    public async Task DetalleExistenteDevuelveGeoJsonCorrecto()
    {
        const double longitud = -63.1823;
        const double latitud = -17.7834;
        var geometria = SpatialGeoJsonHelper.ToGeoJsonObject(
            new GeometryFactory(new PrecisionModel(), 4326)
                .CreatePoint(new Coordinate(longitud, latitud)));
        var repository = new CadastreRepositoryFalso
        {
            DetalleRespuesta = new CodigoFijoDetalleDto
            {
                IdCodigo = 25,
                CodFSql = 9,
                CodFSig = "CF-25",
                CodFijo = 100,
                Nombre = "Plaza",
                Estado = 3,
                FechaCambioEstado = new DateTime(2026, 9, 10),
                IdLote = 4,
                Longitud = longitud,
                Latitud = latitud,
                Geometria = geometria
            }
        };

        var resultado = await CrearControlador(repository).Obtener(25, CancellationToken.None);

        var detalle = Assert.IsType<CodigoFijoDetalleDto>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("Cortado", detalle.EstadoDescripcion);
        Assert.Equal(longitud, detalle.Longitud);
        Assert.Equal(latitud, detalle.Latitud);
        var json = JsonSerializer.SerializeToElement(detalle.Geometria);
        Assert.Equal("Point", json.GetProperty("type").GetString());
        var coordenadas = json.GetProperty("coordinates");
        Assert.Equal(longitud, coordenadas[0].GetDouble());
        Assert.Equal(latitud, coordenadas[1].GetDouble());
        Assert.Equal(25, repository.IdRecibido);
    }

    [Fact(DisplayName = "CU13: detalle inexistente devuelve 404")]
    public async Task DetalleInexistenteDevuelveNotFound()
    {
        var resultado = await CrearControlador(new CadastreRepositoryFalso())
            .Obtener(999, CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ObjectResult>(resultado).StatusCode);
    }

    [Fact(DisplayName = "CU13: valores nulos del detalle se serializan sin error")]
    public async Task DetalleConNulosSeSerializa()
    {
        var repository = new CadastreRepositoryFalso
        {
            DetalleRespuesta = new CodigoFijoDetalleDto
            {
                IdCodigo = 7,
                Estado = 5,
                FechaCambioEstado = new DateTime(2026, 1, 1)
            }
        };

        var resultado = await CrearControlador(repository).Obtener(7, CancellationToken.None);
        var detalle = Assert.IsType<CodigoFijoDetalleDto>(Assert.IsType<OkObjectResult>(resultado).Value);

        Assert.Null(detalle.CodFSql);
        Assert.Null(detalle.CodFSig);
        Assert.Null(detalle.CodFijo);
        Assert.Null(detalle.Nombre);
        Assert.Null(detalle.IdLote);
        Assert.Null(detalle.Longitud);
        Assert.Null(detalle.Latitud);
        Assert.Null(detalle.Geometria);
        Assert.Equal("Baja Total", detalle.EstadoDescripcion);
        var json = JsonSerializer.Serialize(detalle, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        Assert.Contains("\"codFSql\":null", json);
        Assert.Contains("\"codFSig\":null", json);
        Assert.Contains("\"geometria\":null", json);
    }

    [Theory(DisplayName = "CU13: un fallo de bitacora no falla la consulta principal")]
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

    [Fact(DisplayName = "CU13: registra busqueda y detalle con modulo y accion requeridos")]
    public async Task RegistraBitacoraDeAmbasConsultas()
    {
        var bitacora = new BitacoraServiceFalso();
        var repository = new CadastreRepositoryFalso
        {
            DetalleRespuesta = new CodigoFijoDetalleDto
            {
                IdCodigo = 17,
                Estado = 1,
                FechaCambioEstado = DateTime.UtcNow
            }
        };
        var controller = CrearControlador(repository, bitacora: bitacora);

        await controller.Buscar(cancellationToken: CancellationToken.None);
        await controller.Obtener(17, CancellationToken.None);

        Assert.Equal(2, bitacora.Entradas.Count);
        Assert.All(bitacora.Entradas, entrada =>
        {
            Assert.Equal(ModulosSistema.ConsultasYFiltros, entrada.Modulo);
            Assert.Equal("Consultar Código Fijo", entrada.Accion);
            Assert.Equal("CodigosFijos", entrada.Entidad);
        });
        Assert.Null(bitacora.Entradas[0].IdEntidad);
        Assert.Equal(17, bitacora.Entradas[1].IdEntidad);
    }
}
