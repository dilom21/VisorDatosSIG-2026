namespace VisorDatosSIG.Application.DTOs.Authentication;

/// <summary>
/// Credenciales y datos del usuario obtenidos de SQL Server.
/// </summary>
/// <remarks>
/// DTO de uso interno entre Application e Infrastructure: contiene el hash y el salt,
/// por lo que NUNCA debe exponerse en una respuesta HTTP ni registrarse en bitácora o logs.
/// </remarks>
public sealed class UsuarioCredencialesDto
{
    /// <summary>Identificador del usuario.</summary>
    public required int IdUsuario { get; init; }

    /// <summary>Login del usuario.</summary>
    public required string Login { get; init; }

    /// <summary>Nombre completo del usuario.</summary>
    public required string Nombre { get; init; }

    /// <summary>Hash PBKDF2 almacenado (no exponer).</summary>
    public required byte[] PasswordHash { get; init; }

    /// <summary>Salt almacenada (no exponer).</summary>
    public required byte[] PasswordSalt { get; init; }

    /// <summary>Cantidad de iteraciones configurada para el usuario.</summary>
    public required int Iteraciones { get; init; }

    /// <summary>Indica si el usuario está habilitado.</summary>
    public required bool Activo { get; init; }

    /// <summary>Roles activos asociados al usuario.</summary>
    public required IReadOnlyList<string> Roles { get; init; }
}
