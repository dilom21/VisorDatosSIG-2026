using System.Security.Cryptography;
using System.Text;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Users;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Authentication;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas del servicio de autenticación con repositorios simulados (sin SQL Server).
/// </summary>
public sealed class AuthenticationServiceTests
{
    private const string PasswordValida = "Admin123!";
    private const int Iteraciones = 100000;

    private sealed class UsuarioRepositoryFalso(UsuarioCredencialesDto? usuario) : IUsuarioRepository
    {
        public string? UltimoLoginConsultado { get; private set; }

        public Task<UsuarioCredencialesDto?> ObtenerPorLoginAsync(string login, CancellationToken cancellationToken = default)
        {
            UltimoLoginConsultado = login;

            var encontrado = usuario is not null && string.Equals(usuario.Login, login, StringComparison.OrdinalIgnoreCase)
                ? usuario
                : null;

            return Task.FromResult(encontrado);
        }

        public Task<bool> CambiarPasswordAsync(int idUsuario, string actual, string nueva, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<IEnumerable<UserResponseDto>> ObtenerTodosAsync(CancellationToken ct = default) =>
            Task.FromResult<IEnumerable<UserResponseDto>>([]);

        public Task<UserResponseDto?> ObtenerPorIdAsync(int idUsuario, CancellationToken ct = default) =>
            Task.FromResult<UserResponseDto?>(null);

        public Task<UserResponseDto?> CrearUsuarioAsync(CreateUserRequestDto dto, CancellationToken ct = default) =>
            Task.FromResult<UserResponseDto?>(null);

        public Task<bool> CambiarEstadoAsync(int idUsuario, bool activo, CancellationToken ct = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<string>> ObtenerRolesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<UserResponseDto?> ActualizarUsuarioAsync(int idUsuario, UpdateUserRequestDto dto, CancellationToken ct = default) =>
            Task.FromResult<UserResponseDto?>(null);

        public Task<bool> RestablecerPasswordAsync(int idUsuario, string nuevaPassword, CancellationToken ct = default) =>
            Task.FromResult(false);
    }

    private sealed class BitacoraRepositoryFalso : IBitacoraRepository
    {
        public List<BitacoraRegistroDto> Registros { get; } = [];

        public Task RegistrarAsync(BitacoraRegistroDto registro, CancellationToken cancellationToken = default)
        {
            Registros.Add(registro);
            return Task.CompletedTask;
        }
    }

    private sealed class TokenServiceFalso : ITokenService
    {
        public int VecesGenerado { get; private set; }

        public TokenGeneradoDto GenerarToken(AuthenticatedUserDto usuario)
        {
            VecesGenerado++;
            return new TokenGeneradoDto { AccessToken = "token-de-prueba", ExpiraEnSegundos = 1800 };
        }
    }

    private static UsuarioCredencialesDto CrearUsuario(
        string password,
        bool activo = true,
        int iteraciones = Iteraciones,
        params string[] roles)
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iteraciones, HashAlgorithmName.SHA256, 32);

        return new UsuarioCredencialesDto
        {
            IdUsuario = 1,
            Login = "admin",
            Nombre = "Administrador",
            PasswordHash = hash,
            PasswordSalt = salt,
            Iteraciones = iteraciones,
            Activo = activo,
            Roles = roles.Length == 0 ? ["Administrador"] : roles
        };
    }

    private static (AuthenticationService Servicio, BitacoraRepositoryFalso Bitacora, TokenServiceFalso Token) CrearServicio(
        UsuarioCredencialesDto? usuario)
    {
        var bitacora = new BitacoraRepositoryFalso();
        var token = new TokenServiceFalso();

        var servicio = new AuthenticationService(
            new UsuarioRepositoryFalso(usuario),
            new Pbkdf2PasswordHasher(),
            token,
            bitacora);

        return (servicio, bitacora, token);
    }

    private static LoginRequestDto Solicitud(string login = "admin", string password = PasswordValida) =>
        new() { Login = login, Password = password };

    [Fact(DisplayName = "Login: credenciales correctas devuelven token, roles y bitácora exitosa")]
    public async Task CredencialesCorrectasDevuelvenToken()
    {
        var usuario = CrearUsuario(PasswordValida, roles: ["Administrador"]);
        var (servicio, bitacora, token) = CrearServicio(usuario);

        var resultado = await servicio.IniciarSesionAsync(Solicitud(), "127.0.0.1");

        Assert.True(resultado.IsSuccess);
        Assert.Equal(AuthenticationStatus.Success, resultado.Status);
        Assert.NotNull(resultado.Response);
        Assert.Equal("token-de-prueba", resultado.Response.AccessToken);
        Assert.Equal(1800, resultado.Response.ExpiresIn);
        Assert.Equal(1, resultado.Response.Usuario.IdUsuario);
        Assert.Equal("admin", resultado.Response.Usuario.Login);
        Assert.Equal("Administrador", resultado.Response.Usuario.Nombre);
        Assert.Equal(["Administrador"], resultado.Response.Usuario.Roles);
        Assert.Equal(1, token.VecesGenerado);

        var registro = Assert.Single(bitacora.Registros);
        Assert.Equal(BitacoraEventos.ModuloAutenticacion, registro.Modulo);
        Assert.Equal(BitacoraEventos.AccionInicioSesion, registro.Accion);
        Assert.Equal(BitacoraEventos.ResultadoExitoso, registro.Resultado);
        Assert.Equal(1, registro.IdUsuario);
        Assert.Equal("127.0.0.1", registro.Ip);
    }

    [Fact(DisplayName = "Login: usuario inexistente devuelve UserNotFound y registra intento fallido")]
    public async Task UsuarioInexistenteDevuelveUserNotFound()
    {
        var (servicio, bitacora, token) = CrearServicio(null);

        var resultado = await servicio.IniciarSesionAsync(Solicitud(login: "noexiste"));

        Assert.False(resultado.IsSuccess);
        Assert.Equal(AuthenticationStatus.UserNotFound, resultado.Status);
        Assert.Null(resultado.Response);
        Assert.Equal(0, token.VecesGenerado);

        var registro = Assert.Single(bitacora.Registros);
        Assert.Equal(BitacoraEventos.ResultadoFallido, registro.Resultado);
        Assert.Null(registro.IdUsuario);
    }

    [Fact(DisplayName = "Login: usuario desconectado (activo: false) puede iniciar sesión y pasa a conectado")]
    public async Task UsuarioInactivoNoPuedeIniciarSesion()
    {
        var usuario = CrearUsuario(PasswordValida, activo: false);
        var (servicio, bitacora, token) = CrearServicio(usuario);

        var resultado = await servicio.IniciarSesionAsync(Solicitud());

        Assert.Equal(AuthenticationStatus.Success, resultado.Status);
        Assert.NotNull(resultado.Response);
        Assert.Equal(1, token.VecesGenerado);

        var registro = Assert.Single(bitacora.Registros);
        Assert.Equal(BitacoraEventos.ResultadoExitoso, registro.Resultado);
        Assert.Equal(1, registro.IdUsuario);
    }

    [Fact(DisplayName = "Login: contraseña incorrecta devuelve InvalidCredentials y no genera token")]
    public async Task ContrasenaIncorrectaDevuelveInvalidCredentials()
    {
        var usuario = CrearUsuario(PasswordValida);
        var (servicio, bitacora, token) = CrearServicio(usuario);

        var resultado = await servicio.IniciarSesionAsync(Solicitud(password: "Admin123"));

        Assert.Equal(AuthenticationStatus.InvalidCredentials, resultado.Status);
        Assert.Null(resultado.Response);
        Assert.Equal(0, token.VecesGenerado);

        var registro = Assert.Single(bitacora.Registros);
        Assert.Equal(BitacoraEventos.ResultadoFallido, registro.Resultado);
        Assert.Equal(BitacoraEventos.AccionInicioSesion, registro.Accion);
    }

    [Fact(DisplayName = "Login: el login recibido se normaliza con Trim antes de consultarlo")]
    public async Task ElLoginSeNormaliza()
    {
        var usuario = CrearUsuario(PasswordValida);
        var repositorio = new UsuarioRepositoryFalso(usuario);
        var bitacora = new BitacoraRepositoryFalso();

        var servicio = new AuthenticationService(repositorio, new Pbkdf2PasswordHasher(), new TokenServiceFalso(), bitacora);

        var resultado = await servicio.IniciarSesionAsync(Solicitud(login: "   admin   "));

        Assert.True(resultado.IsSuccess);
        Assert.Equal("admin", repositorio.UltimoLoginConsultado);
    }

    [Fact(DisplayName = "Bitácora: nunca se registran contraseña, hash, salt ni token")]
    public async Task LaBitacoraNoContieneDatosSensibles()
    {
        var usuario = CrearUsuario(PasswordValida);
        var (servicio, bitacora, _) = CrearServicio(usuario);

        await servicio.IniciarSesionAsync(Solicitud(), "::1");
        await servicio.IniciarSesionAsync(Solicitud(password: "clave-equivocada"), "::1");
        await servicio.CerrarSesionAsync(1, "admin", "::1");

        var textos = bitacora.Registros
            .SelectMany(registro => new[]
            {
                registro.Modulo,
                registro.Accion,
                registro.Resultado,
                registro.Detalle ?? string.Empty,
                registro.Ip ?? string.Empty
            })
            .ToArray();

        Assert.DoesNotContain(textos, texto => texto.Contains(PasswordValida, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(textos, texto => texto.Contains("clave-equivocada", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(textos, texto => texto.Contains("token-de-prueba", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(textos, texto => texto.Contains(Convert.ToHexString(usuario.PasswordHash), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(textos, texto => texto.Contains(Convert.ToHexString(usuario.PasswordSalt), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(textos, texto => texto.Contains(Convert.ToBase64String(usuario.PasswordHash), StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "Cierre de sesión: registra CIERRE_SESION exitoso")]
    public async Task CierreDeSesionRegistraElEvento()
    {
        var (servicio, bitacora, _) = CrearServicio(CrearUsuario(PasswordValida));

        var resultado = await servicio.CerrarSesionAsync(1, "admin", "10.0.0.5");

        Assert.True(resultado);

        var registro = Assert.Single(bitacora.Registros);
        Assert.Equal(BitacoraEventos.AccionCierreSesion, registro.Accion);
        Assert.Equal(BitacoraEventos.ResultadoExitoso, registro.Resultado);
        Assert.Equal(1, registro.IdUsuario);
        Assert.Equal("10.0.0.5", registro.Ip);
    }
}
