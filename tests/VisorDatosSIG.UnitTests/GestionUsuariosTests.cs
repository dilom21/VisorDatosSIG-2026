using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using VisorDatosSIG.Api.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VisorDatosSIG.Api.Controllers;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.DTOs.Users;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.UnitTests;

public sealed class GestionUsuariosTests
{
    private sealed class Repository : IUsuarioRepository
    {
        public bool Exito { get; set; } = true;
        public bool Activo { get; set; } = true;
        public int? IdAfectado { get; private set; }
        public CreateUserRequestDto? Creado { get; private set; }
        private static UserResponseDto Usuario(int id) => new(id, "consulta", "Consultor", true, DateTime.UtcNow, ["Consultor"]);
        public Task<UsuarioCredencialesDto?> ObtenerPorLoginAsync(string login, CancellationToken ct = default) => Task.FromResult<UsuarioCredencialesDto?>(null);
        public Task<bool> CambiarPasswordAsync(int id, string actual, string nueva, CancellationToken ct = default) { IdAfectado = id; return Task.FromResult(Exito); }
        public Task<IEnumerable<UserResponseDto>> ObtenerTodosAsync(CancellationToken ct = default) => Task.FromResult<IEnumerable<UserResponseDto>>([Usuario(7)]);
        public Task<UserResponseDto?> ObtenerPorIdAsync(int id, CancellationToken ct = default) => Task.FromResult<UserResponseDto?>(Exito ? Usuario(id) with { Activo = Activo } : null);
        public Task<UserResponseDto?> CrearUsuarioAsync(CreateUserRequestDto dto, CancellationToken ct = default) { Creado = dto; return Task.FromResult<UserResponseDto?>(Exito ? Usuario(7) : null); }
        public Task<bool> CambiarEstadoAsync(int id, bool activo, CancellationToken ct = default) { IdAfectado = id; return Task.FromResult(Exito); }
        public Task<IReadOnlyList<string>> ObtenerRolesAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<string>>(["Administrador", "Consultor"]);
        public Task<UserResponseDto?> ActualizarUsuarioAsync(int id, UpdateUserRequestDto dto, CancellationToken ct = default) { IdAfectado = id; return Task.FromResult<UserResponseDto?>(Exito ? Usuario(id) with { Activo = Activo } : null); }
        public Task<bool> RestablecerPasswordAsync(int id, string nueva, CancellationToken ct = default) { IdAfectado = id; return Task.FromResult(Exito); }
    }
    private sealed class Bitacora : IBitacoraRepository
    {
        public List<BitacoraRegistroDto> Registros { get; } = [];
        public Task RegistrarAsync(BitacoraRegistroDto dto, CancellationToken ct = default) { Registros.Add(dto); return Task.CompletedTask; }
    }
    /// <summary>
    /// Resolutor de permisos de prueba: concede o deniega el acceso y el permiso sobre cualquier
    /// opción de menú, igual que <c>dbo.RolMenu</c> en producción.
    /// </summary>
    private sealed class PermisoServiceFalso(bool puede) : IPermisoService
    {
        public Task<PermisosEfectivosDto> ObtenerPermisosAsync(int idUsuario, CancellationToken ct = default) =>
            Task.FromResult(new PermisosEfectivosDto { TieneAccesoWeb = puede, AccesoTotal = puede });
        public Task<bool> TieneAccesoWebAsync(int idUsuario, CancellationToken ct = default) => Task.FromResult(puede);
        public Task<bool> TienePermisoAsync(int idUsuario, string menuUrl, AccionPermiso accion, CancellationToken ct = default) => Task.FromResult(puede);
        public Task<bool> PuedeVerAsync(int idUsuario, string menuUrl, CancellationToken ct = default) => Task.FromResult(puede);
    }
    private static T Contexto<T>(T controller, string? sub = "3") where T : ControllerBase
    {
        var claims = sub is null ? Array.Empty<Claim>() : [new Claim("sub", sub)];
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test")) } };
        return controller;
    }
    /// <summary>
    /// Construye el controlador de usuarios con el permiso dinámico indicado (concedido por
    /// omisión) para las pruebas de autorización y de contrato HTTP.
    /// </summary>
    private static UsuariosController Api(IUsuarioRepository repo, IBitacoraRepository log, bool permitido = true, string? sub = "3") =>
        Contexto(new UsuariosController(repo, log, new PermisoServiceFalso(permitido), NullLogger<UsuariosController>.Instance), sub);
    private static bool Valido(object dto)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var context = new ActionContext { HttpContext = new DefaultHttpContext { RequestServices = provider } };
        provider.GetRequiredService<IObjectModelValidator>().Validate(context, null, string.Empty, dto);
        return context.ModelState.IsValid;
    }

    [Fact]
    public void UsuariosExigeTokenYPermisoDinamicoYPerfilRequiereSesion()
    {
        var autorizacion = typeof(UsuariosController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(autorizacion);
        // La autorización dejó de depender del rol Administrador: la API resuelve el permiso
        // efectivo sobre la opción de menú /Usuarios de dbo.RolMenu en cada petición.
        Assert.Null(autorizacion!.Roles);
        Assert.Contains(typeof(UsuariosController).GetConstructors().SelectMany(c => c.GetParameters()),
            p => p.ParameterType == typeof(IPermisoService));
        Assert.NotNull(typeof(PerfilController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.DoesNotContain(typeof(UsuariosController).GetMethods(), m => m.GetCustomAttribute<AllowAnonymousAttribute>() != null);
    }
    [Fact]
    public async Task SinPermisoDelModuloRespondeProhibidoYSinBitacora()
    {
        var log = new Bitacora(); var c = Api(new Repository(), log, permitido: false);
        var prohibido = Assert.IsType<ObjectResult>(await c.ObtenerTodos(default));
        Assert.Equal(StatusCodes.Status403Forbidden, prohibido.StatusCode);
        Assert.IsType<ProblemDetails>(prohibido.Value);
        Assert.IsType<ObjectResult>(await c.ObtenerRoles(default));
        Assert.IsType<ObjectResult>(await c.ObtenerPorId(7, default));
        Assert.IsType<ObjectResult>(await c.Crear(new("consulta", "Consultor", "Inicial123", ["Consultor"]), default));
        Assert.IsType<ObjectResult>(await c.Actualizar(7, new("Consultor", ["Consultor"]), default));
        Assert.IsType<ObjectResult>(await c.CambiarEstado(7, new(false), default));
        Assert.IsType<ObjectResult>(await c.RestablecerPassword(7, new("Nueva1234"), default));
        Assert.Empty(log.Registros);
    }
    [Fact]
    public async Task SinIdentidadValidaRespondeNoAutorizado()
    {
        var repo = new Repository(); var c = Api(repo, new Bitacora(), sub: null);
        Assert.IsType<UnauthorizedResult>(await c.ObtenerTodos(default));
        Assert.IsType<UnauthorizedResult>(await c.Crear(new("consulta", "Consultor", "Inicial123", ["Consultor"]), default));
        Assert.Null(repo.Creado); Assert.Null(repo.IdAfectado);
    }
    [Theory]
    [InlineData(null)] [InlineData("0")] [InlineData("-1")] [InlineData("abc")]
    public async Task PasswordSinIdentidadValidaNoTocaRepositorio(string? sub)
    {
        var repo = new Repository();
        var c = Contexto(new PerfilController(repo, new Bitacora()), sub);
        Assert.IsType<UnauthorizedResult>(await c.CambiarPassword(new("Actual123", "Nueva1234", "Nueva1234"), default));
        Assert.Null(repo.IdAfectado);
    }
    [Fact]
    public async Task PasswordSoloModificaCuentaDelTokenYRegistraSinSecretos()
    {
        var repo = new Repository(); var log = new Bitacora(); var c = Contexto(new PerfilController(repo, log));
        Assert.IsType<OkObjectResult>(await c.CambiarPassword(new("Actual123", "Nueva1234", "Nueva1234"), default));
        Assert.Equal(3, repo.IdAfectado);
        var registro = Assert.Single(log.Registros);
        Assert.Equal(3, registro.IdUsuario); Assert.Equal(3, registro.IdEntidad);
        Assert.Equal(BitacoraEventos.ResultadoExitoso, registro.Resultado);
        Assert.DoesNotContain("Actual123", JsonSerializer.Serialize(registro));
        Assert.DoesNotContain("Nueva1234", JsonSerializer.Serialize(registro));
    }
    [Fact]
    public async Task PasswordActualIncorrectaRegistraFallo()
    {
        var repo = new Repository { Exito = false }; var log = new Bitacora();
        var c = Contexto(new PerfilController(repo, log));
        Assert.IsType<BadRequestObjectResult>(await c.CambiarPassword(new("Actual123", "Nueva1234", "Nueva1234"), default));
        Assert.Equal(BitacoraEventos.ResultadoFallido, Assert.Single(log.Registros).Resultado);
    }
    [Theory]
    [InlineData("Actual123", "Actual123", "Actual123")]
    [InlineData("Actual123", "Nueva1234", "Otra1234")]
    [InlineData("Actual123", "corta", "corta")]
    [InlineData("", "Nueva1234", "Nueva1234")]
    public async Task PasswordInvalidaNoEsPersistida(string actual, string nueva, string confirmar)
    {
        var repo = new Repository(); var c = Contexto(new PerfilController(repo, new Bitacora()));
        Assert.IsType<BadRequestObjectResult>(await c.CambiarPassword(new(actual, nueva, confirmar), default));
        Assert.Null(repo.IdAfectado);
    }
    [Fact]
    public async Task CrearNormalizaLoginYRolesYRegistraActorYAfectado()
    {
        var repo = new Repository(); var log = new Bitacora(); var c = Api(repo, log);
        Assert.IsType<CreatedAtActionResult>(await c.Crear(new(" consulta ", " Consultor ", "Inicial123", ["Consultor", "Consultor"]), default));
        Assert.Equal("consulta", repo.Creado!.Login); Assert.Equal("Consultor", repo.Creado.Nombre);
        Assert.Single(repo.Creado.Roles);
        var registro = Assert.Single(log.Registros); Assert.Equal(3, registro.IdUsuario); Assert.Equal(7, registro.IdEntidad);
        Assert.DoesNotContain("Inicial123", JsonSerializer.Serialize(registro));
    }
    [Fact]
    public async Task CrearConLoginDuplicadoNoRegistraExito()
    {
        var log = new Bitacora(); var c = Api(new Repository { Exito = false }, log);
        Assert.IsType<BadRequestObjectResult>(await c.Crear(new("consulta", "Consultor", "Inicial123", ["Consultor"]), default));
        Assert.Empty(log.Registros);
    }
    [Theory]
    [InlineData("Inexistente")] [InlineData("")]
    public async Task RolesDesconocidosNoSePersisten(string rol)
    {
        var repo = new Repository(); var c = Api(repo, new Bitacora());
        Assert.IsType<BadRequestObjectResult>(await c.Crear(new("consulta", "Consultor", "Inicial123", [rol]), default));
        Assert.Null(repo.Creado);
        Assert.IsType<BadRequestObjectResult>(await c.Actualizar(7, new("Consultor", [rol]), default));
        Assert.Null(repo.IdAfectado);
    }
    [Fact]
    public async Task RolesVaciosNoSonAceptados()
    {
        var c = Api(new Repository(), new Bitacora());
        Assert.IsType<BadRequestObjectResult>(await c.Crear(new("consulta", "Consultor", "Inicial123", []), default));
        Assert.IsType<BadRequestObjectResult>(await c.Actualizar(7, new("Consultor", []), default));
    }
    [Fact]
    public async Task UsuarioInexistenteDevuelve404SinBitacoraExitosa()
    {
        var log = new Bitacora(); var c = Api(new Repository { Exito = false }, log);
        Assert.IsType<NotFoundObjectResult>(await c.ObtenerPorId(7, default));
        Assert.IsType<NotFoundObjectResult>(await c.Actualizar(7, new("Consultor", ["Consultor"]), default));
        Assert.IsType<NotFoundObjectResult>(await c.CambiarEstado(7, new(false), default));
        Assert.IsType<NotFoundObjectResult>(await c.RestablecerPassword(7, new("Nueva1234"), default));
        Assert.Empty(log.Registros);
    }
    [Fact]
    public async Task OperacionesAdministrativasRegistranActorDistintoDelAfectado()
    {
        var log = new Bitacora(); var c = Api(new Repository(), log);
        Assert.IsType<OkObjectResult>(await c.Actualizar(7, new("Consultor", ["Consultor"]), default));
        Assert.IsType<OkObjectResult>(await c.CambiarEstado(7, new(false), default));
        Assert.IsType<NoContentResult>(await c.RestablecerPassword(7, new("Nueva1234"), default));
        Assert.Equal(3, log.Registros.Count);
        Assert.All(log.Registros, r => { Assert.Equal(3, r.IdUsuario); Assert.Equal(7, r.IdEntidad); Assert.DoesNotContain("Nueva1234", JsonSerializer.Serialize(r)); });
    }
    [Fact]
    public void DtosValidanLongitudesCamposYConfirmacion()
    {
        Assert.True(Valido(new ChangePasswordRequestDto("Actual123", "Nueva1234", "Nueva1234")));
        Assert.False(Valido(new ChangePasswordRequestDto("Actual123", "Nueva1234", "Otra12345")));
        Assert.False(Valido(new ChangePasswordRequestDto(null!, "Nueva1234", "Nueva1234")));
        Assert.False(Valido(new ResetPasswordRequestDto(new string('a', 201))));
        Assert.False(Valido(new CreateUserRequestDto(new string('a', 101), "Nombre", "Inicial123", ["Consultor"])));
        Assert.False(Valido(new CreateUserRequestDto("consulta", " ", "Inicial123", ["Consultor"])));
        Assert.False(Valido(new UpdateUserRequestDto("Nombre", [])));
        Assert.False(Valido(new UpdateUserRequestDto(new string('a', 241), ["Consultor"])));
    }
    [Fact]
    public void RespuestaUsuariosNoExponeCredenciales()
    {
        var json = JsonSerializer.Serialize(new UserResponseDto(7, "consulta", "Nombre", true, DateTime.UtcNow, ["Consultor"]));
        Assert.DoesNotContain("Password", json); Assert.DoesNotContain("Salt", json); Assert.DoesNotContain("Iteraciones", json);
    }
    [Theory]
    [InlineData(true, true, "3", false)]
    [InlineData(true, false, "3", true)]
    [InlineData(false, true, "3", true)]
    [InlineData(true, true, "0", true)]
    [InlineData(true, true, "abc", true)]
    public async Task TokenExistenteRespetaEstadoYRolesActuales(bool existe, bool activo, string sub, bool falla)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUsuarioRepository>(new Repository { Exito = existe, Activo = activo });
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        var context = new TokenValidatedContext(http,
            new AuthenticationScheme("Bearer", null, typeof(JwtBearerHandler)), new JwtBearerOptions())
        {
            Principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", sub), new Claim("role", "Administrador"), new Claim("name", "Nombre anterior")
            ], "Bearer", "name", "role"))
        };
        await SessionTokenValidator.ValidateAsync(context);
        if (falla)
        {
            Assert.NotNull(context.Result!.Failure);
        }
        else
        {
            Assert.False(context.Principal.IsInRole("Administrador"));
            Assert.True(context.Principal.IsInRole("Consultor"));
            Assert.Equal("Consultor", context.Principal.Identity!.Name);
        }
    }

}
