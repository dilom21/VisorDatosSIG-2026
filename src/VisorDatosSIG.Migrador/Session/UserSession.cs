using VisorDatosSIG.Application.DTOs.Authentication;

namespace VisorDatosSIG.Migrador.Session;

/// <summary>
/// Sesión del usuario autenticado, mantenida únicamente en memoria.
/// </summary>
/// <remarks>
/// El token se guarda sin el prefijo <c>Bearer</c>: ese prefijo lo agrega
/// <c>AuthenticationApiClient</c> al construir la cabecera <c>Authorization</c>.
/// La sesión desaparece cuando se cierra la aplicación: no se escribe en disco,
/// en el registro de Windows, en SQL local ni en la bitácora.
/// </remarks>
public sealed class UserSession
{
    private static readonly string[] SinRoles = [];

    private const string PrefijoBearer = "Bearer ";

    private string? _accessToken;

    /// <summary>Token JWT de acceso (solo en memoria) o <c>null</c> si no hay sesión.</summary>
    public string? AccessToken => _accessToken;

    /// <summary>Identificador del usuario autenticado.</summary>
    public int? IdUsuario { get; private set; }

    /// <summary>Login del usuario autenticado.</summary>
    public string Login { get; private set; } = string.Empty;

    /// <summary>Nombre completo del usuario autenticado.</summary>
    public string Nombre { get; private set; } = string.Empty;

    /// <summary>Roles activos del usuario autenticado.</summary>
    public IReadOnlyList<string> Roles { get; private set; } = SinRoles;

    /// <summary>Instante en que vence el token; <c>null</c> si no hay sesión.</summary>
    public DateTimeOffset? ExpiraEn { get; private set; }

    /// <summary>Instante de expiración de la sesión.</summary>
    public DateTimeOffset? Expiration => ExpiraEn;

    /// <summary>Indica si existe una sesión con token de acceso.</summary>
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_accessToken);

    /// <summary>Nombre listo para mostrar en la cabecera del Migrador.</summary>
    public string NombreParaMostrar => string.IsNullOrWhiteSpace(Nombre) ? Login : Nombre;

    /// <summary>Primer rol del usuario o «-» cuando no tiene roles.</summary>
    public string RolPrincipal => Roles.Count > 0 ? Roles[0] : "-";

    /// <summary>Texto con todos los roles separados por coma.</summary>
    public string RolesTexto => Roles.Count > 0 ? string.Join(", ", Roles) : "-";

    /// <summary>Iniciales del usuario para el avatar (por ejemplo «A» o «JC»).</summary>
    public string Iniciales => CalcularIniciales(NombreParaMostrar, Login);

    /// <summary>Se produce cuando cambia el estado de la sesión (inicio, actualización o cierre).</summary>
    public event EventHandler? EstadoCambiado;

    /// <summary>
    /// Guarda los datos de una sesión recién iniciada.
    /// </summary>
    /// <param name="accessToken">Token JWT devuelto por la API.</param>
    /// <param name="usuario">Datos públicos del usuario autenticado.</param>
    /// <param name="expiresInSegundos">Vigencia del token en segundos.</param>
    public void Establecer(string accessToken, AuthenticatedUserDto usuario, int expiresInSegundos)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        var token = NormalizarToken(accessToken);
        if (token.Length == 0)
        {
            throw new ArgumentException("El token de acceso no puede estar vacío.", nameof(accessToken));
        }

        _accessToken = token;
        AplicarUsuario(usuario);
        ExpiraEn = expiresInSegundos > 0
            ? DateTimeOffset.Now.AddSeconds(expiresInSegundos)
            : DateTimeOffset.Now.AddMinutes(30);

        EstadoCambiado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Actualiza los datos públicos del usuario sin modificar el token.
    /// </summary>
    /// <param name="usuario">Datos públicos del usuario autenticado.</param>
    public void ActualizarUsuario(AuthenticatedUserDto usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);

        if (!IsAuthenticated)
        {
            return;
        }

        AplicarUsuario(usuario);
        EstadoCambiado?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Elimina por completo la sesión en memoria (token incluido).
    /// </summary>
    public void Clear()
    {
        var habiaSesion = IsAuthenticated || IdUsuario is not null;

        _accessToken = null;
        IdUsuario = null;
        Login = string.Empty;
        Nombre = string.Empty;
        Roles = SinRoles;
        ExpiraEn = null;

        if (habiaSesion)
        {
            EstadoCambiado?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Quita un prefijo «Bearer » escrito por error para no duplicarlo en la cabecera.</summary>
    public static string NormalizarToken(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return string.Empty;
        }

        var token = accessToken.Trim();

        if (token.StartsWith(PrefijoBearer, StringComparison.OrdinalIgnoreCase))
        {
            token = token[PrefijoBearer.Length..].Trim();
        }
        else if (string.Equals(token, PrefijoBearer.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            // Un texto formado solo por la palabra «Bearer» no es un token válido.
            token = string.Empty;
        }

        return token;
    }

    /// <summary>Calcula las iniciales a partir del nombre y, si hace falta, del login.</summary>
    public static string CalcularIniciales(string? nombre, string? login)
    {
        var baseTexto = string.IsNullOrWhiteSpace(nombre) ? login : nombre;
        if (string.IsNullOrWhiteSpace(baseTexto))
        {
            return "?";
        }

        var partes = baseTexto.Trim()
            .Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (partes.Length == 0)
        {
            return "?";
        }

        if (partes.Length == 1)
        {
            return char.ToUpperInvariant(partes[0][0]).ToString();
        }

        return string.Concat(
            char.ToUpperInvariant(partes[0][0]),
            char.ToUpperInvariant(partes[1][0]));
    }

    private void AplicarUsuario(AuthenticatedUserDto usuario)
    {
        IdUsuario = usuario.IdUsuario;
        Login = usuario.Login ?? string.Empty;
        Nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? Login : usuario.Nombre;
        Roles = usuario.Roles is { Count: > 0 } roles ? [.. roles] : SinRoles;
    }
}
