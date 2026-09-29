using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Migrador.Session;

namespace VisorDatosSIG.Migrador.Services;

/// <summary>
/// Cliente HTTP del Migrador contra los endpoints de autenticación de VisorDatosSIG.Api.
/// </summary>
/// <remarks>
/// <para>
/// Es el único punto de la aplicación de escritorio que conoce las rutas de autenticación y
/// construye la cabecera <c>Authorization</c>. El Migrador nunca se conecta a SQL Server ni
/// conoce la cadena de conexión, la clave JWT, hashes o salts.
/// </para>
/// <para>
/// No lanza excepciones de red: cada operación devuelve un <see cref="ResultadoApi{T}"/> con un
/// mensaje listo para la interfaz. Los detalles internos (trazas, JSON técnico, excepciones)
/// nunca se propagan al usuario.
/// </para>
/// <para>
/// El <see cref="HttpClient"/> se recibe ya construido y se reutiliza durante toda la vida de la
/// aplicación: no se crea un cliente nuevo por petición.
/// </para>
/// </remarks>
public sealed class AuthenticationApiClient
{
    /// <summary>Ruta del inicio de sesión.</summary>
    public const string RutaIniciarSesion = "api/autenticacion/iniciar";

    /// <summary>Ruta de consulta del usuario autenticado.</summary>
    public const string RutaUsuarioActual = "api/autenticacion/me";

    /// <summary>Ruta del cierre de sesión.</summary>
    public const string RutaCerrarSesion = "api/autenticacion/cerrar";

    private const string EsquemaAutenticacion = "Bearer";

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _cliente;

    /// <summary>
    /// Inicializa el cliente con el <see cref="HttpClient"/> reutilizable de la aplicación.
    /// </summary>
    /// <param name="cliente">Cliente HTTP con la dirección base de la API ya configurada.</param>
    public AuthenticationApiClient(HttpClient cliente) =>
        _cliente = cliente ?? throw new ArgumentNullException(nameof(cliente));

    /// <summary>Dirección base de la API en uso (solo informativa).</summary>
    public Uri? DireccionBase => _cliente.BaseAddress;

    /// <summary>
    /// Se produce cuando una petición protegida devuelve 401: la sesión en memoria dejó de ser válida.
    /// </summary>
    public event EventHandler? SesionExpirada;

    /// <summary>
    /// Inicia sesión con login y contraseña.
    /// </summary>
    /// <param name="login">Usuario.</param>
    /// <param name="password">Contraseña en texto plano (solo viaja por HTTP, nunca se registra).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Token de acceso y datos públicos del usuario, o el mensaje de error correspondiente.</returns>
    public Task<ResultadoApi<LoginResponseDto>> IniciarSesionAsync(
        string login,
        string password,
        CancellationToken cancellationToken = default)
    {
        var cuerpo = JsonSerializer.Serialize(new LoginRequestDto { Login = login, Password = password }, OpcionesJson);

        var solicitud = new HttpRequestMessage(HttpMethod.Post, RutaIniciarSesion)
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };

        return EjecutarAsync<LoginResponseDto>(solicitud, cancellationToken);
    }

    /// <summary>
    /// Consulta los datos públicos del usuario autenticado (<c>GET /api/autenticacion/me</c>).
    /// </summary>
    /// <param name="accessToken">Token de acceso de la sesión actual.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Datos del usuario o el estado del error (401 se informa como sesión expirada).</returns>
    public Task<ResultadoApi<AuthenticatedUserDto>> ObtenerUsuarioActualAsync(
        string? accessToken,
        CancellationToken cancellationToken = default) =>
        EjecutarAsync<AuthenticatedUserDto>(
            new HttpRequestMessage(HttpMethod.Get, RutaUsuarioActual),
            cancellationToken,
            accessToken,
            requiereAutenticacion: true);

    /// <summary>
    /// Registra el cierre de sesión en la API (<c>POST /api/autenticacion/cerrar</c>).
    /// </summary>
    /// <param name="accessToken">Token de acceso de la sesión actual.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// Resultado del cierre remoto. Aunque falle la comunicación, el llamador debe cerrar la
    /// sesión local: el cierre de sesión del Migrador nunca depende del servidor.
    /// </returns>
    public Task<ResultadoApi<bool>> CerrarSesionAsync(
        string? accessToken,
        CancellationToken cancellationToken = default) =>
        EjecutarAsync<bool>(
            new HttpRequestMessage(HttpMethod.Post, RutaCerrarSesion),
            cancellationToken,
            accessToken,
            requiereAutenticacion: true,
            avisarSesionExpirada: false);

    /// <summary>
    /// Construye la cabecera <c>Authorization</c> agregando el prefijo «Bearer » una única vez.
    /// </summary>
    /// <param name="accessToken">Token de acceso, con o sin el prefijo «Bearer ».</param>
    /// <returns>Cabecera lista para usar o <c>null</c> si no hay token.</returns>
    public static AuthenticationHeaderValue? CrearAutorizacion(string? accessToken)
    {
        var token = UserSession.NormalizarToken(accessToken);

        return token.Length == 0
            ? null
            : new AuthenticationHeaderValue(EsquemaAutenticacion, token);
    }

    private async Task<ResultadoApi<T>> EjecutarAsync<T>(
        HttpRequestMessage solicitud,
        CancellationToken cancellationToken,
        string? accessToken = null,
        bool requiereAutenticacion = false,
        bool avisarSesionExpirada = true)
    {
        using (solicitud)
        {
            try
            {
                if (requiereAutenticacion)
                {
                    var autorizacion = CrearAutorizacion(accessToken);
                    if (autorizacion is null)
                    {
                        // Sin token no se molesta al servidor: la sesión ya no es utilizable.
                        return Fallo<T>(EstadoOperacion.SesionExpirada, MensajesAutenticacion.SesionExpirada);
                    }

                    solicitud.Headers.Authorization = autorizacion;
                }

                using var respuesta = await _cliente.SendAsync(
                    solicitud,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

                if (respuesta.IsSuccessStatusCode)
                {
                    return await LeerRespuestaExitosaAsync<T>(respuesta, cancellationToken);
                }

                return InterpretarError<T>(respuesta.StatusCode, requiereAutenticacion, avisarSesionExpirada);
            }
            catch (OperationCanceledException)
            {
                // Se distingue la cancelación pedida por la aplicación del tiempo de espera agotado.
                return cancellationToken.IsCancellationRequested
                    ? Fallo<T>(EstadoOperacion.Cancelado, MensajesAutenticacion.Cancelado)
                    : Fallo<T>(EstadoOperacion.TiempoAgotado, MensajesAutenticacion.TiempoAgotado);
            }
            catch (HttpRequestException)
            {
                return Fallo<T>(EstadoOperacion.ServidorNoDisponible, MensajesAutenticacion.ServidorNoDisponible);
            }
            catch (Exception)
            {
                // Nunca se expone el detalle interno: la interfaz recibe siempre un mensaje genérico.
                return Fallo<T>(EstadoOperacion.ErrorServidor, MensajesAutenticacion.ErrorServidor);
            }
        }
    }

    private ResultadoApi<T> InterpretarError<T>(HttpStatusCode codigo, bool esPeticionProtegida, bool avisarSesionExpirada)
    {
        switch ((int)codigo)
        {
            case 400:
                return Fallo<T>(EstadoOperacion.SolicitudInvalida, MensajesAutenticacion.SolicitudInvalida);

            case 401 when esPeticionProtegida:
                if (avisarSesionExpirada)
                {
                    SesionExpirada?.Invoke(this, EventArgs.Empty);
                }

                return Fallo<T>(EstadoOperacion.SesionExpirada, MensajesAutenticacion.SesionExpirada);

            case 401:
                return Fallo<T>(EstadoOperacion.CredencialesInvalidas, MensajesAutenticacion.CredencialesInvalidas);

            case 403 when esPeticionProtegida:
                return Fallo<T>(EstadoOperacion.SinPermisos, MensajesAutenticacion.SinPermisos);

            case 403:
                return Fallo<T>(EstadoOperacion.CuentaInhabilitada, MensajesAutenticacion.CuentaInhabilitada);

            default:
                return Fallo<T>(EstadoOperacion.ErrorServidor, MensajesAutenticacion.ErrorServidor);
        }
    }

    private static async Task<ResultadoApi<T>> LeerRespuestaExitosaAsync<T>(
        HttpResponseMessage respuesta,
        CancellationToken cancellationToken)
    {
        // Las respuestas sin contenido (204) solo confirman la operación.
        if (respuesta.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(bool))
        {
            return new ResultadoApi<T>
            {
                Estado = EstadoOperacion.Exito,
                Mensaje = MensajesAutenticacion.OperacionCompletada,
                Datos = (T)(object)true
            };
        }

        try
        {
            var json = await respuesta.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Fallo<T>(EstadoOperacion.ErrorServidor, MensajesAutenticacion.RespuestaInesperada);
            }

            var datos = JsonSerializer.Deserialize<T>(json, OpcionesJson);

            return datos is null
                ? Fallo<T>(EstadoOperacion.ErrorServidor, MensajesAutenticacion.RespuestaInesperada)
                : new ResultadoApi<T>
                {
                    Estado = EstadoOperacion.Exito,
                    Mensaje = MensajesAutenticacion.OperacionCompletada,
                    Datos = datos
                };
        }
        catch (JsonException)
        {
            return Fallo<T>(EstadoOperacion.ErrorServidor, MensajesAutenticacion.RespuestaInesperada);
        }
        catch (OperationCanceledException)
        {
            return cancellationToken.IsCancellationRequested
                ? Fallo<T>(EstadoOperacion.Cancelado, MensajesAutenticacion.Cancelado)
                : Fallo<T>(EstadoOperacion.TiempoAgotado, MensajesAutenticacion.TiempoAgotado);
        }
    }

    private static ResultadoApi<T> Fallo<T>(EstadoOperacion estado, string mensaje) =>
        new() { Estado = estado, Mensaje = mensaje };
}
