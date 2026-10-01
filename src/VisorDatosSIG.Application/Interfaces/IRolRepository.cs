using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Security;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato de persistencia de roles (<c>dbo.Roles</c>) y de su matriz de permisos
/// (<c>dbo.RolMenu</c>).
/// </summary>
/// <remarks>
/// Reglas transversales de la implementación:
/// <list type="bullet">
/// <item>Toda escritura y su evento de auditoría se confirman en <b>una sola transacción</b>: no
/// puede quedar un cambio de permisos sin su registro en <c>dbo.Bitacora</c> ni al revés.</item>
/// <item>Los roles nunca se eliminan físicamente; se deshabilitan
/// (<c>dbo.Roles.Estado = 0</c>) para conservar el historial.</item>
/// <item>El nombre del rol es único según <c>UQ_Roles_NombreRol</c>; el repositorio informa el
/// duplicado mediante el resultado de la operación, no mediante excepciones de SQL.</item>
/// </list>
/// </remarks>
public interface IRolRepository
{
    /// <summary>
    /// Obtiene todos los roles registrados con la cantidad de usuarios activos asignados.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<IReadOnlyList<RolFilaDto>> ObtenerRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un rol por identificador.
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El rol; <c>null</c> cuando no existe.</returns>
    Task<RolFilaDto?> ObtenerRolPorIdAsync(int idRol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si ya existe otro rol con el mismo nombre (comparación sin distinción de mayúsculas
    /// ni acentos).
    /// </summary>
    /// <param name="nombreRol">Nombre a verificar.</param>
    /// <param name="idRolExcluido">Rol que se está editando y debe excluirse de la comparación.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<bool> ExisteNombreRolAsync(
        string nombreRol,
        int? idRolExcluido,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un rol habilitado y su evento de auditoría en la misma transacción.
    /// </summary>
    /// <param name="nombreRol">Nombre del rol.</param>
    /// <param name="descripcion">Descripción opcional.</param>
    /// <param name="crearAuditoria">
    /// Genera el evento de bitácora a registrar junto con la creación. Recibe el identificador
    /// recién generado por la base de datos, que no se conoce antes de la inserción.
    /// </param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Identificador del rol creado.</returns>
    Task<int> CrearRolAsync(
        string nombreRol,
        string? descripcion,
        Func<int, BitacoraRegistroDto> crearAuditoria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza nombre y descripción de un rol y registra el evento de auditoría en la misma
    /// transacción.
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="nombreRol">Nuevo nombre.</param>
    /// <param name="descripcion">Nueva descripción.</param>
    /// <param name="auditoria">Evento de bitácora a registrar junto con la modificación.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>false</c> cuando el rol ya no existe.</returns>
    Task<bool> ActualizarRolAsync(
        int idRol,
        string nombreRol,
        string? descripcion,
        BitacoraRegistroDto auditoria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cambia el estado lógico del rol y registra el evento de auditoría en la misma transacción.
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="activo">Estado deseado.</param>
    /// <param name="auditoria">Evento de bitácora a registrar junto con el cambio.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>false</c> cuando el rol ya no existe.</returns>
    Task<bool> ActualizarEstadoRolAsync(
        int idRol,
        bool activo,
        BitacoraRegistroDto auditoria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene las filas de <c>dbo.RolMenu</c> del rol (solo las que otorgan al menos un permiso).
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<IReadOnlyList<PermisoMenuFilaDto>> ObtenerPermisosRolAsync(
        int idRol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si <b>todos</b> los identificadores indicados corresponden a menús activos.
    /// </summary>
    /// <param name="idsMenu">Identificadores de menú de la petición.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<bool> ExistenMenusActivosAsync(
        IReadOnlyList<int> idsMenu,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reemplaza la matriz de permisos del rol y registra el evento de auditoría en la misma
    /// transacción.
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="permisos">Filas que otorgan al menos un permiso; el resto se elimina.</param>
    /// <param name="auditoria">Evento de bitácora a registrar junto con la actualización.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>false</c> cuando el rol ya no existe.</returns>
    Task<bool> GuardarPermisosRolAsync(
        int idRol,
        IReadOnlyList<PermisoMenuFilaDto> permisos,
        BitacoraRegistroDto auditoria,
        CancellationToken cancellationToken = default);
}
