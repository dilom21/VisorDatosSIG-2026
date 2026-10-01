using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Security;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato de resolución de permisos contra <c>dbo.UsuariosRoles</c>,
/// <c>dbo.RolMenu</c> y <c>dbo.MenuOpciones</c>.
/// </summary>
/// <remarks>
/// La autorización se resuelve por el <b>nombre</b> del rol y por la <b>Url</b> del menú, nunca por
/// <c>IdRol</c> ni <c>IdMenu</c>: esas columnas son <c>IDENTITY</c> y pueden variar entre bases
/// de datos. El rol del migrador no participa de la seguridad web (lo excluye el servicio).
/// </remarks>
public interface IPermisoRepository
{
    /// <summary>
    /// Obtiene los roles <b>activos</b> asignados al usuario.
    /// </summary>
    /// <param name="idUsuario">Identificador del usuario autenticado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Roles activos del usuario; colección vacía cuando no tiene ninguno.</returns>
    Task<IReadOnlyList<RolAsignadoDto>> ObtenerRolesActivosAsync(
        int idUsuario,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si alguno de los roles indicados otorga la acción sobre el menú de la ruta.
    /// </summary>
    /// <param name="idsRoles">Identificadores de los roles activos del usuario.</param>
    /// <param name="menuUrl">Ruta de <c>dbo.MenuOpciones.Url</c> que se desea autorizar.</param>
    /// <param name="accion">Acción solicitada sobre el módulo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>true</c> cuando al menos un rol otorga la acción.</returns>
    Task<bool> TienePermisoAsync(
        IReadOnlyList<int> idsRoles,
        string menuUrl,
        AccionPermiso accion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los identificadores de los menús activos que alguno de los roles puede ver.
    /// </summary>
    /// <param name="idsRoles">Identificadores de los roles activos del usuario.</param>
    /// <param name="accion">Acción a proyectar (habitualmente <see cref="AccionPermiso.Ver"/>).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Identificadores de menú; la unión de todos los roles indicados.</returns>
    Task<IReadOnlyList<int>> ObtenerMenusConPermisoAsync(
        IReadOnlyList<int> idsRoles,
        AccionPermiso accion,
        CancellationToken cancellationToken = default);
}
