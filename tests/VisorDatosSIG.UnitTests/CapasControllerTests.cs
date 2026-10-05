using System.Globalization;
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
using VisorDatosSIG.Application.DTOs.CodigosFijos;
using VisorDatosSIG.Application.DTOs.Manzanas;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas unitarias para CU09 - Gestionar Elementos Geográfico en CapasController, incluida la
/// autorización dinámica del visor: token JWT obligatorio y permiso de ver sobre <c>/Visor</c>
/// resuelto con <see cref="IPermisoService"/> (no una lista fija de roles).
/// </summary>
public sealed class CapasControllerTests
{
    private sealed class CadastreRepositoryFalso : ICadastreRepository
    {
        public Task<PagedResult<CodigoFijoResumenDto>> SearchCodigosFijosAsync(
            CodigoFijoConsultaDto consulta,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<CodigoFijoResumenDto>
            {
                Pagina = consulta.Pagina,
                Limite = consulta.Limite
            });

        public Task<CodigoFijoDetalleDto?> GetCodigoFijoByIdAsync(
            int idCodigo,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<CodigoFijoDetalleDto?>(null);

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

        public string? UltimaCapaConsultada { get; private set; }
        public int UltimoLimit { get; private set; }
        public double? UltimoMinX { get; private set; }
        public double? UltimoMinY { get; private set; }
        public double? UltimoMaxX { get; private set; }
        public double? UltimoMaxY { get; private set; }

        /// <summary>
        /// Cantidad de lecturas catastrales efectivamente ejecutadas por los endpoints del visor.
        /// Permite comprobar que una petición sin permiso no llega a consultar la base de datos.
        /// </summary>
        public int ConsultasRealizadas { get; private set; }

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
            ConsultasRealizadas++;
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
            ConsultasRealizadas++;

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
            ConsultasRealizadas++;
            UltimoLngIdentificar = longitude;
            UltimoLatIdentificar = latitude;
            UltimaToleranciaIdentificar = toleranceMeters;
            return Task.FromResult(IdentificarRespuesta);
        }

        public Task<double[]?> GetLayerExtentAsync(string? layerName = null, CancellationToken cancellationToken = default)
        {
            ConsultasRealizadas++;
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

    /// <summary>
    /// Resolutor de permisos simulado: registra la consulta recibida para comprobar que la
    /// autorización del visor se decide por el permiso de ver sobre <c>/Visor</c> y no por el
    /// nombre del rol declarado en el token.
    /// </summary>
    private sealed class PermisoServiceFalso(bool puedeVer) : IPermisoService
    {
        /// <summary>Ruta de menú consultada en la última comprobación de permiso.</summary>
        public string? UrlConsultada { get; private set; }

        /// <summary>Acción consultada en la última comprobación de permiso.</summary>
        public AccionPermiso? AccionConsultada { get; private set; }

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
            CancellationToken cancellationToken = default)
        {
            UrlConsultada = menuUrl;
            AccionConsultada = accion;

            return Task.FromResult(puedeVer);
        }

        public Task<bool> PuedeVerAsync(int idUsuario, string menuUrl, CancellationToken cancellationToken = default)
        {
            UrlConsultada = menuUrl;
            AccionConsultada = AccionPermiso.Ver;

            return Task.FromResult(puedeVer);
        }
    }


    /// <summary>
    /// Construye el controlador con la identidad del token JWT y el resolutor de permisos indicado.
    /// </summary>
    /// <param name="cadastreRepo">Repositorio catastral simulado.</param>
    /// <param name="bitacoraService">Servicio de bitácora simulado.</param>
    /// <param name="idUsuario">Identificador del claim <c>sub</c>; <c>null</c> simula un token sin identidad válida.</param>
    /// <param name="rol">
    /// Rol declarado en el token. Es informativo: la autorización del visor ya no depende de una lista
    /// fija de roles, sino del permiso de ver sobre <c>/Visor</c> resuelto por el servicio de permisos.
    /// </param>
    /// <param name="puedeVer">Resultado que devuelve el resolutor de permisos.</param>
    /// <param name="permisoService">Resolutor concreto, cuando la prueba necesita inspeccionar la consulta recibida.</param>
    private static CapasController CrearControlador(
        CadastreRepositoryFalso cadastreRepo,
        BitacoraServiceFalso bitacoraService,
        int? idUsuario = 42,
        string rol = "Consultor",
        bool puedeVer = true,
        PermisoServiceFalso? permisoService = null)
    {
        var claims = new List<Claim>();

        if (idUsuario.HasValue)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, idUsuario.Value.ToString(CultureInfo.InvariantCulture)));
        }

        claims.Add(new Claim("rol", rol));

        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var httpContext = new DefaultHttpContext
        {
            User = user
        };
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.100");

        return new CapasController(
            cadastreRepo,
            bitacoraService,
            permisoService ?? new PermisoServiceFalso(puedeVer),
            NullLogger<CapasController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    /// <summary>
    /// Ejecuta el endpoint GET indicado del visor para las pruebas de autorización.
    /// </summary>
    /// <param name="controller">Controlador construido con su identidad y sus permisos.</param>
    /// <param name="endpoint">Clave del endpoint: <c>catalogo</c>, <c>geojson</c>, <c>identificar</c> o <c>extension</c>.</param>
    private static Task<IActionResult> EjecutarEndpointAsync(CapasController controller, string endpoint) =>
        endpoint switch
        {
            "catalogo" => controller.ObtenerCatalogoCapas(CancellationToken.None),
            "geojson" => controller.ObtenerGeoJson("Manzanas"),
            "identificar" => controller.Identificar(lng: -68.15, lat: -16.50),
            _ => controller.ObtenerExtension(capa: "Manzanas")
        };

    [Fact(DisplayName = "Capas API: exige autenticación con Authorize general, sin lista fija de roles")]
    public void ExigeAutenticacionSinListaFijaDeRoles()
    {
        var autorizacion = typeof(CapasController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(autorizacion);

        // La autorización de la API web es dinámica (dbo.RolMenu): la lista fija de roles quedó
        // obsoleta porque un rol nuevo con PuedeVer sobre /Visor también debe poder usar el visor.
        Assert.Null(autorizacion!.Roles);
        Assert.Null(autorizacion.Policy);
    }

    [Fact(DisplayName = "Capas API: expone los cuatro endpoints GET del visor")]
    public void ExponeLosEndpointsDelVisor()
    {
        var tipo = typeof(CapasController);

        Assert.Equal("api/[controller]", tipo.GetCustomAttribute<RouteAttribute>()!.Template);

        Assert.NotNull(tipo.GetMethod(nameof(CapasController.ObtenerCatalogoCapas))!.GetCustomAttribute<HttpGetAttribute>());
        Assert.Equal(
            "{capa}/geojson",
            tipo.GetMethod(nameof(CapasController.ObtenerGeoJson))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal(
            "identificar",
            tipo.GetMethod(nameof(CapasController.Identificar))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
        Assert.Equal(
            "extension",
            tipo.GetMethod(nameof(CapasController.ObtenerExtension))!.GetCustomAttribute<HttpGetAttribute>()!.Template);
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

    [Fact(DisplayName = "Capas API: el GeoJSON exige y consulta el permiso de ver sobre /Visor")]
    public async Task ObtenerGeoJson_ConsultaElPermisoDeVerSobreElVisor()
    {
        var repo = new CadastreRepositoryFalso();
        var permiso = new PermisoServiceFalso(puedeVer: true);
        var controller = CrearControlador(repo, new BitacoraServiceFalso(), permisoService: permiso);

        var resultado = await controller.ObtenerGeoJson("Manzanas");

        Assert.IsType<OkObjectResult>(resultado);

        // La autorización se resuelve por la ruta del módulo, no por el nombre del rol.
        Assert.Equal(PermisoMenu.Visor, permiso.UrlConsultada);
        Assert.Equal(AccionPermiso.Ver, permiso.AccionConsultada);
        Assert.Equal(1, repo.ConsultasRealizadas);
    }

    [Fact(DisplayName = "Capas API: un rol personalizado con PuedeVer sobre /Visor entra al visor")]
    public async Task RolPersonalizado_ConPuedeVerEnElVisor_DevuelveOk()
    {
        var repo = new CadastreRepositoryFalso();
        var controller = CrearControlador(repo, new BitacoraServiceFalso(), rol: "AnalistaCatastro", puedeVer: true);

        var resultado = await controller.ObtenerCatalogoCapas(CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(1, repo.ConsultasRealizadas);
    }

    [Fact(DisplayName = "Capas API: un usuario solo con rol del migrador y sin permiso web recibe 403")]
    public async Task RolDelMigrador_SinPermisoWeb_DevuelveForbidden()
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso();
        var controller = CrearControlador(repo, bitacora, rol: "RESPONSABLE MIGRADOR", puedeVer: false);

        var resultado = await controller.ObtenerCatalogoCapas(CancellationToken.None);

        var prohibido = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, prohibido.StatusCode);
        Assert.IsType<ProblemDetails>(prohibido.Value);
        Assert.Equal(0, repo.ConsultasRealizadas);
        Assert.Empty(bitacora.EntradasRegistradas);
    }

    [Theory(DisplayName = "Capas API: 403 sin permiso de ver sobre /Visor, sin consultar datos ni bitácora")]
    [InlineData("catalogo")]
    [InlineData("geojson")]
    [InlineData("identificar")]
    [InlineData("extension")]
    public async Task SinPermisoDeVer_DevuelveForbidden(string endpoint)
    {
        var repo = new CadastreRepositoryFalso();
        var bitacora = new BitacoraServiceFalso();
        var permiso = new PermisoServiceFalso(puedeVer: false);
        var controller = CrearControlador(repo, bitacora, permisoService: permiso);

        var resultado = await EjecutarEndpointAsync(controller, endpoint);

        var prohibido = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, prohibido.StatusCode);
        Assert.IsType<ProblemDetails>(prohibido.Value);
        Assert.Equal(PermisoMenu.Visor, permiso.UrlConsultada);

        // El rechazo ocurre antes de cualquier operación: no se lee el catastral ni se audita.
        Assert.Equal(0, repo.ConsultasRealizadas);
        Assert.Empty(bitacora.EntradasRegistradas);
    }

    [Fact(DisplayName = "Capas API: 401 cuando el token no identifica al usuario")]
    public async Task SinIdentidad_DevuelveNoAutorizado()
    {
        var controller = CrearControlador(
            new CadastreRepositoryFalso(),
            new BitacoraServiceFalso(),
            idUsuario: null);

        var resultado = await controller.ObtenerCatalogoCapas(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(resultado);
    }
}
