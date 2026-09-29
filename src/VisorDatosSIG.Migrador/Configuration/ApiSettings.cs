using System.Net.Http.Headers;

namespace VisorDatosSIG.Migrador.Configuration;

/// <summary>
/// Configuración de acceso a la API HTTP del VisorDatosSIG.
/// </summary>
/// <remarks>
/// La dirección de la API se define en este único lugar y puede cambiarse sin recompilar
/// mediante la variable de entorno <see cref="VariableEntornoUrl"/>.
/// La dirección no es un secreto: el Migrador nunca conoce la cadena de conexión de SQL Server,
/// la clave JWT ni credenciales internas del servidor.
/// </remarks>
public sealed class ApiSettings
{
    /// <summary>Variable de entorno que sobrescribe la dirección de la API.</summary>
    public const string VariableEntornoUrl = "VISORDATOSSIG_API_URL";

    /// <summary>Dirección predeterminada de la API en desarrollo local.</summary>
    public const string UrlPredeterminada = "http://localhost:5080";

    private static readonly TimeSpan EsperaPredeterminada = TimeSpan.FromSeconds(15);

    private ApiSettings(Uri baseUri, TimeSpan timeout, string? advertencia)
    {
        BaseUri = baseUri;
        Timeout = timeout;
        Advertencia = advertencia;
    }

    /// <summary>Dirección base de la API, siempre terminada en «/».</summary>
    public Uri BaseUri { get; }

    /// <summary>Dirección base de la API en texto (solo para mostrar al usuario o registrar en bitácora).</summary>
    public string BaseUrl => BaseUri.AbsoluteUri;

    /// <summary>Tiempo máximo de espera de cada petición HTTP.</summary>
    public TimeSpan Timeout { get; }

    /// <summary>Aviso de configuración cuando la variable de entorno no es válida; <c>null</c> si todo está correcto.</summary>
    public string? Advertencia { get; }

    /// <summary>Configuración predeterminada: API local en desarrollo.</summary>
    public static ApiSettings Predeterminada { get; } = new(new Uri(UrlPredeterminada + "/", UriKind.Absolute), EsperaPredeterminada, null);

    /// <summary>
    /// Crea la configuración leyendo la variable de entorno <see cref="VariableEntornoUrl"/>.
    /// </summary>
    /// <param name="leerVariable">
    /// Función de lectura de variables de entorno; se puede sustituir en pruebas.
    /// Si es <c>null</c> se usa <see cref="Environment.GetEnvironmentVariable(string)"/>.
    /// </param>
    /// <returns>Configuración efectiva: la de la variable de entorno o la predeterminada.</returns>
    public static ApiSettings CrearDesdeEntorno(Func<string, string?>? leerVariable = null)
    {
        leerVariable ??= Environment.GetEnvironmentVariable;

        var valor = leerVariable(VariableEntornoUrl);
        if (string.IsNullOrWhiteSpace(valor))
        {
            return Predeterminada;
        }

        if (IntentarNormalizar(valor, out var baseUri))
        {
            return new ApiSettings(baseUri, EsperaPredeterminada, null);
        }

        return new ApiSettings(
            Predeterminada.BaseUri,
            EsperaPredeterminada,
            $"La variable de entorno {VariableEntornoUrl} no contiene una dirección http(s) válida: se usará {UrlPredeterminada}.");
    }

    /// <summary>
    /// Valida y normaliza una dirección de API agregando la barra final.
    /// </summary>
    /// <param name="valor">Dirección escrita por el usuario.</param>
    /// <param name="baseUri">Dirección normalizada cuando la validación es correcta.</param>
    /// <returns><c>true</c> si la dirección es absoluta y usa el esquema http o https.</returns>
    public static bool IntentarNormalizar(string? valor, out Uri baseUri)
    {
        baseUri = Predeterminada.BaseUri;

        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var texto = valor.Trim().TrimEnd('/');
        if (!Uri.TryCreate(texto + "/", UriKind.Absolute, out var candidata))
        {
            return false;
        }

        if (!string.Equals(candidata.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidata.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        baseUri = candidata;
        return true;
    }

    /// <summary>
    /// Crea el <see cref="HttpClient"/> reutilizable de la aplicación con esta configuración.
    /// </summary>
    /// <remarks>Debe crearse una sola vez y liberarse al cerrar la aplicación.</remarks>
    public HttpClient CrearClienteHttp()
    {
        var cliente = new HttpClient
        {
            BaseAddress = BaseUri,
            Timeout = Timeout
        };

        cliente.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return cliente;
    }
}
