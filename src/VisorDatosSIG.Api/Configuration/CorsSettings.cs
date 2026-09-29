using Microsoft.Net.Http.Headers;

namespace VisorDatosSIG.Api.Configuration;

/// <summary>
/// Configuración de CORS de la API (sección <c>Cors</c> de la configuración).
/// </summary>
/// <remarks>
/// La lista de orígenes autorizados se lee de <c>Cors:AllowedOrigins</c>. En desarrollo se define en
/// <c>Properties/launchSettings.json</c> con las variables <c>Cors__AllowedOrigins__0</c>,
/// <c>Cors__AllowedOrigins__1</c>, ... (archivo versionado y sin secretos) o en
/// <c>appsettings.Development.json</c> (no versionado). Nunca se habilita <c>AllowAnyOrigin</c>:
/// si la lista queda vacía, la política no autoriza ningún origen.
/// </remarks>
public sealed class CorsSettings
{
    /// <summary>Nombre de la sección de configuración.</summary>
    public const string SectionName = "Cors";

    /// <summary>Nombre de la política CORS que aplica la API (visor web).</summary>
    public const string NombrePolitica = "VisorWeb";

    /// <summary>Clave de configuración con la lista de orígenes autorizados.</summary>
    public const string ClaveAllowedOrigins = SectionName + ":AllowedOrigins";

    /// <summary>Encabezados que los clientes autorizados pueden enviar (incluye Authorization).</summary>
    public static readonly string[] EncabezadosPermitidos =
    [
        HeaderNames.Authorization,
        HeaderNames.ContentType,
        HeaderNames.Accept
    ];

    /// <summary>Métodos HTTP autorizados para los clientes configurados.</summary>
    public static readonly string[] MetodosPermitidos =
    [
        HttpMethods.Get,
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete,
        HttpMethods.Options
    ];

    /// <summary>Orígenes autorizados, normalizados y sin duplicados.</summary>
    public string[] AllowedOrigins { get; set; } = [];

    /// <summary>Indica si hay al menos un origen autorizado configurado.</summary>
    public bool TieneOrigenes => AllowedOrigins.Length > 0;

    /// <summary>
    /// Lee la sección <c>Cors</c> de la configuración.
    /// </summary>
    /// <param name="configuracion">Configuración de la aplicación.</param>
    /// <returns>Configuración de CORS con los orígenes ya normalizados.</returns>
    public static CorsSettings Desde(IConfiguration configuracion)
    {
        var valores = configuracion.GetSection(SectionName).GetSection("AllowedOrigins").Get<string[]>();

        return new CorsSettings { AllowedOrigins = Normalizar(valores) };
    }

    /// <summary>
    /// Valida la configuración y devuelve el motivo cuando no es utilizable.
    /// </summary>
    /// <remarks>
    /// Una lista vacía es válida: significa que ningún origen navegador está autorizado
    /// (no se aplica un respaldo con comodines).
    /// </remarks>
    /// <returns>Mensaje de error o <c>null</c> cuando la configuración es válida.</returns>
    public string? Validar()
    {
        foreach (var origen in AllowedOrigins)
        {
            if (origen == "*")
            {
                return $"'{ClaveAllowedOrigins}' no admite comodines (\"*\"): indique los orígenes exactos, " +
                       "por ejemplo http://localhost:5000. La API nunca habilita AllowAnyOrigin.";
            }

            if (!EsOrigenValido(origen))
            {
                return $"'{ClaveAllowedOrigins}' contiene un origen con formato inválido: '{origen}'. " +
                       "Use esquema, host y puerto en minúsculas, sin ruta, sin consulta y sin barra final " +
                       "(por ejemplo http://localhost:5000).";
            }
        }

        return null;
    }

    private static string[] Normalizar(IEnumerable<string>? valores)
    {
        var origenes = new List<string>();

        foreach (var valor in valores ?? [])
        {
            var origen = (valor ?? string.Empty).Trim().TrimEnd('/');
            if (origen.Length == 0)
            {
                continue;
            }

            if (!origenes.Contains(origen, StringComparer.OrdinalIgnoreCase))
            {
                origenes.Add(origen);
            }
        }

        return [.. origenes];
    }

    private static bool EsOrigenValido(string origen)
    {
        if (!Uri.TryCreate(origen, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        // El origen debe ser exactamente "esquema://host:puerto": sin ruta, consulta ni fragmento.
        return uri.AbsolutePath == "/"
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);
    }
}
