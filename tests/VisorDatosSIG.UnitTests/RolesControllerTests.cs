using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas del controlador de roles y permisos (<c>/api/roles</c>): contrato HTTP, protección por
/// token, autorización por permiso y traducción de los resultados de negocio.
/// </summary>
public sealed class RolesControllerTests
{
    private sealed class RolesServiceFalso : IRolesService
    {
        private static readonly RolDto Consultor = new()
        {
            IdRol = 5,
            NombreRol = "Consultor",
            Activo = true
        };

        public RolOperacionDto Creacion { get; set; } = RolOperacionDto.Exitoso(Consultor);

        public Task<IReadOnlyList<RolDto>> ObtenerRolesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RolDto>>([Consultor]);

        public Task<RolDto?> ObtenerRolAsync(int idRol, CancellationToken cancellationToken = default) =>
            Task.FromResult<RolDto?>(idRol == Consultor.IdRol ? Consultor : null);

        public Task<RolOperacionDto> CrearRolAsync(
            RolSolicitudDto solicitud,
            ContextoAuditoriaDto auditoria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Creacion);

        public Task<RolOperacionDto> ActualizarRolAsync(
            int idRol,
            RolSolicitudDto solicitud,
            ContextoAuditoriaDto auditoria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RolOperacionDto.Fallido(ResultadoRol.RolProtegido, "Rol protegido."));

        public Task<RolOperacionDto> CambiarEstadoAsync(
            int idRol,
            bool activo,
            ContextoAuditoriaDto auditoria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(RolOperacionDto.Exitoso(Consultor));

        public Task<IReadOnlyList<CatalogoPermisoDto>> ObtenerCatalogoPermisosAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CatalogoPermisoDto>>([]);

        public Task<RolPermisosDto?> ObtenerPermisosAsync(int idRol, CancellationToken cancellationToken = default) =>
            Task.FromResult<RolPermisosDto?>(idRol == Consultor.IdRol
                ? new RolPermisosDto { IdRol = Consultor.IdRol, NombreRol = Consultor.NombreRol }
                : null);

        public Task<RolPermisosOperacionDto> GuardarPermisosAsync(
            int idRol,
            RolPermisosSolicitudDto solicitud,
            ContextoAuditoriaDto auditoria,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                RolPermisosOperacionDto.Fallido(ResultadoRol.RolProtegido, "Rol protegido."));
    }

    private sealed class PermisoServiceFalso(bool puede) : IPermisoService
    {
        public Task<PermisosEfectivosDto> ObtenerPermisosAsync(
            int idUsuario,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PermisosEfectivosDto { TieneAccesoWeb = puede, AccesoTotal = puede });

        public Task<bool> TieneAccesoWebAsync(int idUsuario, CancellationToken cancellationToken = default) =>
            Task.FromResult(puede);

        public Task<bool> TienePermisoAsync(
            int idUsuario,
            string menuUrl,
            AccionPermiso accion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(puede);

        public Task<bool> PuedeVerAsync(int idUsuario, string menuUrl, CancellationToken cancellationToken = default) =>
            Task.FromResult(puede);
    }

    /// <summary>Construye la identidad del token JWT para el usuario indicado (claim <c>sub</c>).</summary>
    private static ClaimsPrincipal Principal(int? idUsuario)
    {
        var reclamos = idUsuario.HasValue
            ? new[] { new Claim("sub", idUsuario.Value.ToString(CultureInfo.InvariantCulture)) }
            : [];

        return new ClaimsPrincipal(new ClaimsIdentity(reclamos, authenticationType: "Prueba"));
    }

    private static RolesController Controlador(
        RolesServiceFalso servicio,
        bool tienePermiso,
        int? idUsuario = 9) =>
        new(servicio, new PermisoServiceFalso(tienePermiso), NullLogger<RolesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = Principal(idUsuario),
                    // Los servicios mínimos de MVC permiten generar la URL del 201 (CreatedAtAction).
                    RequestServices = new ServiceCollection().AddMvcCore().Services.BuildServiceProvider()
                }
            }
        };

    [Fact(DisplayName = "Roles API: expone la ruta api/roles y exige autenticación")]
    public void ExponeLaRutaYExigeAutenticacion()
    {
        var tipo = typeof(RolesController);

        Assert.NotNull(tipo.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(tipo.GetCustomAttribute<ApiControllerAttribute>());

        var ruta = tipo.GetCustomAttribute<RouteAttribute>();
        Assert.NotNull(ruta);
        Assert.Equal("api/roles", ruta!.Template);

        Assert.NotNull(tipo.GetMethod(nameof(RolesController.ObtenerRoles))!.GetCustomAttribute<HttpGetAttribute>());
        Assert.NotNull(tipo.GetMethod(nameof(RolesController.Crear))!.GetCustomAttribute<HttpPostAttribute>());
        Assert.Equal(
            "{idRol:int}",
            tipo.GetMethod(nameof(RolesController.Actualizar))!.GetCustomAttribute<HttpPatchAttribute>()!.Template);
        Assert.Equal(
            "{idRol:int}/estado",
            tipo.GetMethod(nameof(RolesController.CambiarEstado))!.GetCustomAttribute<HttpPatchAttribute>()!.Template);
        Assert.Equal(
            "{idRol:int}/permisos",
            tipo.GetMethod(nameof(RolesController.GuardarPermisos))!.GetCustomAttribute<HttpPutAttribute>()!.Template);
    }

    [Fact(DisplayName = "Roles API: 200 con los roles administrables cuando hay permiso de ver")]
    public async Task DevuelveLosRolesConPermiso()
    {
        var resultado = await Controlador(new RolesServiceFalso(), tienePermiso: true)
            .ObtenerRoles(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var roles = Assert.IsAssignableFrom<IReadOnlyList<RolDto>>(ok.Value);
        Assert.Equal("Consultor", Assert.Single(roles).NombreRol);
    }

    [Fact(DisplayName = "Roles API: 403 cuando falta el permiso del módulo de roles")]
    public async Task DevuelveProhibidoSinPermiso()
    {
        var resultado = await Controlador(new RolesServiceFalso(), tienePermiso: false)
            .ObtenerRoles(CancellationToken.None);

        var prohibido = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status403Forbidden, prohibido.StatusCode);
        Assert.IsType<ProblemDetails>(prohibido.Value);
    }

    [Fact(DisplayName = "Roles API: 401 cuando el token no identifica al usuario")]
    public async Task DevuelveNoAutorizadoSinIdentidad()
    {
        var resultado = await Controlador(new RolesServiceFalso(), tienePermiso: true, idUsuario: null)
            .ObtenerRoles(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(resultado);
    }

    [Fact(DisplayName = "Roles API: crear un rol devuelve 201 con el recurso creado")]
    public async Task DevuelveCreadoAlCrear()
    {
        var resultado = await Controlador(new RolesServiceFalso(), tienePermiso: true)
            .Crear(new RolSolicitudDto { NombreRol = "Consultor" }, CancellationToken.None);

        var creado = Assert.IsType<CreatedAtActionResult>(resultado);
        Assert.Equal(nameof(RolesController.Obtener), creado.ActionName);
        Assert.IsType<RolDto>(creado.Value);
    }

    [Fact(DisplayName = "Roles API: un rol protegido devuelve 409")]
    public async Task DevuelveConflictoConRolProtegido()
    {
        var resultado = await Controlador(new RolesServiceFalso(), tienePermiso: true)
            .Actualizar(5, new RolSolicitudDto { NombreRol = "Nuevo nombre" }, CancellationToken.None);

        var conflicto = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status409Conflict, conflicto.StatusCode);
    }

    [Fact(DisplayName = "Roles API: 404 cuando el rol no se administra desde la web")]
    public async Task DevuelveNoEncontrado()
    {
        var resultado = await Controlador(new RolesServiceFalso(), tienePermiso: true)
            .Obtener(404, CancellationToken.None);

        var noEncontrado = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(StatusCodes.Status404NotFound, noEncontrado.StatusCode);
    }
}
