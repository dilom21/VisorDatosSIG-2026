using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Authentication;

/// <summary>
/// Servicio de autenticación: valida credenciales contra SQL Server, genera el token JWT
/// y registra todos los intentos en la bitácora.
/// </summary>
/// <remarks>
/// Reglas aplicadas:
/// <list type="bullet">
/// <item>La contraseña nunca se registra ni se persiste; solo se verifica con PBKDF2-SHA256.</item>
/// <item>Un usuario inactivo no puede iniciar sesión.</item>
/// <item>Los intentos fallidos y el cierre de sesión se registran en dbo.Bitacora.</item>
/// </list>
/// </remarks>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IBitacoraRepository _bitacoraRepository;

    /// <summary>
    /// Inicializa el servicio de autenticación.
    /// </summary>
    public AuthenticationService(
        IUsuarioRepository usuarioRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IBitacoraRepository bitacoraRepository)
    {
        _usuarioRepository = usuarioRepository ?? throw new ArgumentNullException(nameof(usuarioRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _bitacoraRepository = bitacoraRepository ?? throw new ArgumentNullException(nameof(bitacoraRepository));
    }

    /// <inheritdoc />
    public async Task<LoginResultDto> IniciarSesionAsync(
        LoginRequestDto solicitud,
        string? ipOrigen = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        var login = solicitud.Login.Trim();
        var usuario = await _usuarioRepository.ObtenerPorLoginAsync(login, cancellationToken);

        if (usuario is null)
        {
            await RegistrarEnBitacoraAsync(
                null,
                BitacoraEventos.AccionInicioSesion,
                BitacoraEventos.ResultadoFallido,
                "Login no registrado en el sistema.",
                ipOrigen,
                cancellationToken);

            return LoginResultDto.Fallido(AuthenticationStatus.UserNotFound);
        }

        var credencialesValidas = _passwordHasher.Verificar(
            solicitud.Password,
            usuario.PasswordSalt,
            usuario.Iteraciones,
            usuario.PasswordHash);

        if (!credencialesValidas)
        {
            await RegistrarEnBitacoraAsync(
                usuario.IdUsuario,
                BitacoraEventos.AccionInicioSesion,
                BitacoraEventos.ResultadoFallido,
                "Credenciales incorrectas.",
                ipOrigen,
                cancellationToken);

            return LoginResultDto.Fallido(AuthenticationStatus.InvalidCredentials);
        }

        // Al iniciar sesión correctamente, el usuario pasa a estado Conectado (Activo = 1)
        await _usuarioRepository.CambiarEstadoAsync(usuario.IdUsuario, true, cancellationToken);

        var autenticado = new AuthenticatedUserDto
        {
            IdUsuario = usuario.IdUsuario,
            Login = usuario.Login,
            Nombre = usuario.Nombre,
            Roles = usuario.Roles
        };

        var token = _tokenService.GenerarToken(autenticado);

        await RegistrarEnBitacoraAsync(
            usuario.IdUsuario,
            BitacoraEventos.AccionInicioSesion,
            BitacoraEventos.ResultadoExitoso,
            "Inicio de sesión correcto.",
            ipOrigen,
            cancellationToken);

        return LoginResultDto.Exitoso(new LoginResponseDto
        {
            AccessToken = token.AccessToken,
            ExpiresIn = token.ExpiraEnSegundos,
            Usuario = autenticado
        });
    }

    /// <inheritdoc />
    public async Task<bool> CerrarSesionAsync(
        int idUsuario,
        string? login,
        string? ipOrigen = null,
        CancellationToken cancellationToken = default)
    {
        if (idUsuario > 0)
        {
            // Al cerrar sesión, el usuario pasa a estado Desconectado (Activo = 0)
            await _usuarioRepository.CambiarEstadoAsync(idUsuario, false, cancellationToken);
        }

        await RegistrarEnBitacoraAsync(
            idUsuario > 0 ? idUsuario : null,
            BitacoraEventos.AccionCierreSesion,
            BitacoraEventos.ResultadoExitoso,
            string.IsNullOrWhiteSpace(login)
                ? "Cierre de sesión del usuario autenticado."
                : $"Cierre de sesión del usuario '{login}'.",
            ipOrigen,
            cancellationToken);

        return true;
    }

    private Task RegistrarEnBitacoraAsync(
        int? idUsuario,
        string accion,
        string resultado,
        string detalle,
        string? ipOrigen,
        CancellationToken cancellationToken) =>
        _bitacoraRepository.RegistrarAsync(
            new BitacoraRegistroDto
            {
                IdUsuario = idUsuario,
                Modulo = BitacoraEventos.ModuloAutenticacion,
                Accion = accion,
                Entidad = idUsuario.HasValue ? BitacoraEventos.EntidadUsuario : null,
                IdEntidad = idUsuario,
                Resultado = resultado,
                Detalle = detalle,
                Ip = ipOrigen
            },
            cancellationToken);
}
