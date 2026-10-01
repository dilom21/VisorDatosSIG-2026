using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Authentication;

namespace VisorDatosSIG.Api.Authorization;

/// <summary>
/// Utilidades de identidad, autorización y auditoría compartidas por los controladores web.
/// </summary>
/// <remarks>
/// Centraliza la lectura de la identidad desde el token JWT (nunca desde el cuerpo ni la cadena de
/// consulta) y el chequeo de permisos contra <see cref="IPermisoService"/>, que lee
/// <c>dbo.RolMenu</c> en cada petición: así el cambio de la matriz de un rol tiene efecto inmediato
/// para el usuario afectado, sin obligarlo a iniciar sesión de nuevo.
/// </remarks>
public static class AutorizacionWeb
{
    /// <summary>
    /// Obtiene el identificador del usuario autenticado a partir del claim <c>sub</c>.
    /// </summary>
    /// <param name="usuario">Identidad del token JWT.</param>
    /// <returns>Identificador del usuario; <c>null</c> cuando falta o no es válido.</returns>
    public static int? IdUsuario(this ClaimsPrincipal? usuario)
    {
        var valor = usuario?.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return int.TryParse(valor, out var idUsuario) && idUsuario > 0 ? idUsuario : null;
    }

    /// <summary>
    /// Obtiene el nombre para mostrar del usuario autenticado (<c>name</c> y, si falta, <c>login</c>).
    /// </summary>
    /// <param name="usuario">Identidad del token JWT.</param>
    /// <returns>Nombre del usuario; cadena vacía cuando no se puede determinar.</returns>
    public static string NombreUsuario(this ClaimsPrincipal? usuario) =>
        usuario?.FindFirstValue(JwtTokenService.ClaimNombre)
        ?? usuario?.FindFirstValue(JwtTokenService.ClaimLogin)
        ?? string.Empty;

    /// <summary>
    /// Construye el contexto de auditoría de una operación que se registrará en <c>dbo.Bitacora</c>.
    /// </summary>
    /// <param name="usuario">Identidad del token JWT.</param>
    /// <param name="ip">Dirección IP de origen; <c>null</c> cuando no se pudo determinar.</param>
    /// <returns>Contexto con el usuario y la IP de origen.</returns>
    public static ContextoAuditoriaDto ContextoAuditoria(this ClaimsPrincipal? usuario, string? ip) =>
        new() { IdUsuario = usuario.IdUsuario(), Ip = ip };

    /// <summary>
    /// Obtiene la dirección IP de origen de la petición normalizando el formato IPv4 mapeado.
    /// </summary>
    /// <param name="contexto">Contexto HTTP de la petición; puede ser <c>null</c>.</param>
    /// <returns>Dirección IP; <c>null</c> cuando no se pudo determinar.</returns>
    public static string? IpOrigen(this HttpContext? contexto)
    {
        var direccion = contexto?.Connection.RemoteIpAddress;

        if (direccion is null)
        {
            return null;
        }

        return direccion.IsIPv4MappedToIPv6
            ? direccion.MapToIPv4().ToString()
            : direccion.ToString();
    }

    /// <summary>
    /// Indica si el usuario autenticado puede ejecutar la acción sobre el módulo de la ruta
    /// indicada.
    /// </summary>
    /// <param name="usuario">Identidad del token JWT.</param>
    /// <param name="permisoService">Resolutor de permisos contra <c>dbo.RolMenu</c>.</param>
    /// <param name="menuUrl">Ruta del módulo (constantes de <see cref="PermisoMenu"/>).</param>
    /// <param name="accion">Acción solicitada sobre el módulo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <c>true</c> cuando el usuario tiene el permiso; el administrador siempre lo tiene. Un usuario
    /// sin identidad válida nunca lo tiene.
    /// </returns>
    public static async Task<bool> PuedeAsync(
        this ClaimsPrincipal? usuario,
        IPermisoService permisoService,
        string menuUrl,
        AccionPermiso accion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permisoService);

        var idUsuario = usuario.IdUsuario();

        return idUsuario is not null
            && await permisoService.TienePermisoAsync(idUsuario.Value, menuUrl, accion, cancellationToken);
    }

    /// <summary>
    /// Indica si el usuario autenticado puede consultar (ver) el módulo de la ruta indicada.
    /// </summary>
    /// <param name="usuario">Identidad del token JWT.</param>
    /// <param name="permisoService">Resolutor de permisos contra <c>dbo.RolMenu</c>.</param>
    /// <param name="menuUrl">Ruta del módulo (constantes de <c>PermisoMenu</c>).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// <c>true</c> cuando el usuario tiene permiso de ver; el administrador siempre lo tiene. Un
    /// usuario sin identidad válida nunca lo tiene.
    /// </returns>
    public static Task<bool> PuedeVerAsync(
        this ClaimsPrincipal? usuario,
        IPermisoService permisoService,
        string menuUrl,
        CancellationToken cancellationToken = default) =>
        usuario.PuedeAsync(permisoService, menuUrl, AccionPermiso.Ver, cancellationToken);
}
