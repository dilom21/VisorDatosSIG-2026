namespace VisorDatosSIG.Application.DTOs.Authentication;

/// <summary>
/// Resultado posible del intento de inicio de sesión.
/// </summary>
public enum AuthenticationStatus
{
    /// <summary>Credenciales válidas y usuario activo.</summary>
    Success = 1,

    /// <summary>Credenciales inválidas.</summary>
    InvalidCredentials = 2,

    /// <summary>El usuario existe pero está inactivo.</summary>
    InactiveUser = 3,

    /// <summary>El login no corresponde a ningún usuario.</summary>
    UserNotFound = 4
}

/// <summary>
/// Resultado del inicio de sesión, sin exponer detalles internos.
/// </summary>
public sealed class LoginResultDto
{
    /// <summary>Resultado del intento.</summary>
    public required AuthenticationStatus Status { get; init; }

    /// <summary>Respuesta con token y datos públicos cuando el acceso fue exitoso.</summary>
    public LoginResponseDto? Response { get; init; }

    /// <summary>Indica si el acceso fue exitoso.</summary>
    public bool IsSuccess => Status == AuthenticationStatus.Success;

    /// <summary>
    /// Crea un resultado exitoso.
    /// </summary>
    public static LoginResultDto Exitoso(LoginResponseDto respuesta) => new()
    {
        Status = AuthenticationStatus.Success,
        Response = respuesta
    };

    /// <summary>
    /// Crea un resultado fallido.
    /// </summary>
    public static LoginResultDto Fallido(AuthenticationStatus estado) => new() { Status = estado };
}
