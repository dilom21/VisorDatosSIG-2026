using System.Text;

namespace VisorDatosSIG.Infrastructure.Authentication;

/// <summary>
/// Configuración del token JWT (sección <c>Jwt</c> de la configuración).
/// </summary>
/// <remarks>
/// La clave (<see cref="Key"/>) NO debe versionarse: se configura con User Secrets,
/// una variable de entorno o appsettings.Development.json (no versionado).
/// </remarks>
public sealed class JwtSettings
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Emisor del token.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Audiencia del token.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Clave secreta de firma (HMAC-SHA256, mínimo 32 bytes).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Vigencia del token en minutos.</summary>
    public int ExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Valida la configuración y devuelve el motivo cuando no es utilizable.
    /// </summary>
    /// <returns>Mensaje de error o <c>null</c> cuando la configuración es válida.</returns>
    public string? Validar()
    {
        if (string.IsNullOrWhiteSpace(Issuer))
        {
            return "Falta configurar Jwt:Issuer.";
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            return "Falta configurar Jwt:Audience.";
        }

        if (string.IsNullOrWhiteSpace(Key))
        {
            return "Falta configurar Jwt:Key (no debe quedar en el repositorio).";
        }

        if (Encoding.UTF8.GetByteCount(Key) < 32)
        {
            return "Jwt:Key debe tener al menos 32 bytes para firmar con HMAC-SHA256.";
        }

        if (ExpirationMinutes <= 0)
        {
            return "Jwt:ExpirationMinutes debe ser mayor que cero.";
        }

        return null;
    }
}
