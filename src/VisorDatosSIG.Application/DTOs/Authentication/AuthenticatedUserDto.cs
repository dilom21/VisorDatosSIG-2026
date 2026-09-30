namespace VisorDatosSIG.Application.DTOs.Authentication;

/// <summary>
/// Información pública del usuario autenticado.
/// Nunca incluye hash, salt, iteraciones ni datos internos.
/// </summary>
public sealed class AuthenticatedUserDto
{
    /// <summary>Identificador del usuario.</summary>
    public required int IdUsuario { get; init; }

    /// <summary>Login del usuario.</summary>
    public required string Login { get; init; }

    /// <summary>Nombre completo del usuario.</summary>
    public required string Nombre { get; init; }

    /// <summary>Roles activos del usuario.</summary>
    public required IReadOnlyList<string> Roles { get; init; }
}
