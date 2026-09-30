namespace VisorDatosSIG.Migrador.Services;

/// <summary>
/// Resultado de una operación contra la API: estado de negocio, datos y mensaje para el usuario.
/// </summary>
/// <typeparam name="T">Tipo de los datos devueltos por la API.</typeparam>
/// <remarks>
/// Nunca contiene excepciones, trazas ni JSON técnico: el mensaje está listo para mostrarse
/// en la interfaz del Migrador.
/// </remarks>
public sealed class ResultadoApi<T>
{
    /// <summary>Estado de la operación.</summary>
    public required EstadoOperacion Estado { get; init; }

    /// <summary>Datos devueltos por la API cuando la operación fue exitosa.</summary>
    public T? Datos { get; init; }

    /// <summary>Mensaje apto para el usuario.</summary>
    public required string Mensaje { get; init; }

    /// <summary>Indica si la operación finalizó correctamente.</summary>
    public bool EsExitoso => Estado == EstadoOperacion.Exito;
}

/// <summary>
/// Estado de negocio de una operación contra la API de autenticación.
/// </summary>
public enum EstadoOperacion
{
    /// <summary>Operación completada (HTTP 2xx).</summary>
    Exito,

    /// <summary>Login o contraseña incorrectos (HTTP 401 en el inicio de sesión).</summary>
    CredencialesInvalidas,

    /// <summary>La cuenta existe pero no puede iniciar sesión (HTTP 403 en el inicio de sesión).</summary>
    CuentaInhabilitada,

    /// <summary>La cuenta no tiene permisos para la operación solicitada (HTTP 403 en una petición protegida).</summary>
    SinPermisos,

    /// <summary>Datos de la solicitud inválidos o incompletos (HTTP 400).</summary>
    SolicitudInvalida,

    /// <summary>El token no es válido o expiró, o la petición protegida no lo incluye (HTTP 401).</summary>
    SesionExpirada,

    /// <summary>El servidor respondió con un error (HTTP 5xx o respuesta inesperada).</summary>
    ErrorServidor,

    /// <summary>No fue posible establecer comunicación con el servidor.</summary>
    ServidorNoDisponible,

    /// <summary>El servidor tardó demasiado en responder.</summary>
    TiempoAgotado,

    /// <summary>La operación se canceló desde la aplicación.</summary>
    Cancelado
}

/// <summary>
/// Mensajes que el Migrador muestra al usuario.
/// </summary>
/// <remarks>
/// Se definen una sola vez para evitar textos distintos ante el mismo error y para no exponer
/// nunca detalles internos (trazas, SQL, excepciones).
/// </remarks>
public static class MensajesAutenticacion
{
    /// <summary>Login o contraseña incorrectos.</summary>
    public const string CredencialesInvalidas = "Usuario o contraseña incorrectos.";

    /// <summary>Cuenta deshabilitada (HTTP 403 en el inicio de sesión).</summary>
    public const string CuentaInhabilitada = "No fue posible iniciar sesión con esta cuenta.";

    /// <summary>Cuenta sin permisos para una operación protegida.</summary>
    public const string SinPermisos = "Su cuenta no tiene permisos para realizar esta operación.";

    /// <summary>Datos incompletos o inválidos.</summary>
    public const string SolicitudInvalida = "Revise el usuario y la contraseña ingresados.";

    /// <summary>Sesión vencida o token inválido.</summary>
    public const string SesionExpirada = "Su sesión ha expirado. Inicie sesión nuevamente.";

    /// <summary>Error del servidor.</summary>
    public const string ErrorServidor = "El servidor de autenticación no pudo procesar la solicitud. Intente nuevamente.";

    /// <summary>API no disponible.</summary>
    public const string ServidorNoDisponible = "No fue posible comunicarse con el servidor de autenticación.";

    /// <summary>Tiempo de espera agotado.</summary>
    public const string TiempoAgotado = "El servidor tardó demasiado en responder.";

    /// <summary>Operación cancelada por el usuario.</summary>
    public const string Cancelado = "Se canceló la operación.";

    /// <summary>Respuesta del servidor con un formato inesperado.</summary>
    public const string RespuestaInesperada = "El servidor devolvió una respuesta inesperada.";

    /// <summary>Operación completada correctamente.</summary>
    public const string OperacionCompletada = "Operación completada.";
}
