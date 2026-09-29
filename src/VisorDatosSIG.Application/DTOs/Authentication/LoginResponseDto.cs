namespace VisorDatosSIG.Application.DTOs.Authentication;

/// <summary>
/// Respuesta del inicio de sesión.
/// </summary>
public sealed class LoginResponseDto
{
    /// <summary>Token JWT de acceso.</summary>
    public required string AccessToken { get; init; }

    /// <summary>Vigencia del token expresada en segundos.</summary>
    public required int ExpiresIn { get; init; }

    /// <summary>Información pública del usuario autenticado.</summary>
    public required AuthenticatedUserDto Usuario { get; init; }
}
