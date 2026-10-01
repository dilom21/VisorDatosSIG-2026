using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Security;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato del servicio que resuelve los permisos efectivos del usuario autenticado.
/// </summary>
/// <remarks>
/// Los permisos se leen de la base de datos en cada petición: si un administrador cambia la
/// matriz de permisos de un rol, el cambio tiene efecto en la siguiente petición del usuario
/// afectado sin necesidad de volver a iniciar sesión.
/// <para>
/// El rol <c>Administrador</c> tiene acceso total implícito (<c>dbo.RolMenu</c> no lo limita) y el
/// rol del migrador se ignora: un usuario que solo tenga ese rol no puede acceder al visor web.
/// </para>
/// </remarks>
public interface IPermisoService
{
    /// <summary>
    /// Calcula los permisos efectivos del usuario (acceso web y menús visibles).
    /// </summary>
    /// <param name="idUsuario">Identificador del usuario autenticado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<PermisosEfectivosDto> ObtenerPermisosAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el usuario puede utilizar la aplicación web (tiene al menos un rol web activo).
    /// </summary>
    /// <param name="idUsuario">Identificador del usuario autenticado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<bool> TieneAccesoWebAsync(int idUsuario, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el usuario puede ejecutar la acción indicada en el módulo de la ruta.
    /// </summary>
    /// <param name="idUsuario">Identificador del usuario autenticado.</param>
    /// <param name="menuUrl">Ruta del módulo (por ejemplo <see cref="PermisoMenu.Roles"/>).</param>
    /// <param name="accion">Acción solicitada.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>true</c> cuando el usuario tiene el permiso; el administrador siempre lo tiene.</returns>
    Task<bool> TienePermisoAsync(
        int idUsuario,
        string menuUrl,
        AccionPermiso accion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el usuario puede consultar (ver) el módulo de la ruta.
    /// </summary>
    /// <param name="idUsuario">Identificador del usuario autenticado.</param>
    /// <param name="menuUrl">Ruta del módulo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<bool> PuedeVerAsync(int idUsuario, string menuUrl, CancellationToken cancellationToken = default);
}
