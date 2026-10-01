using VisorDatosSIG.Application.DTOs.Security;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato del servicio de administración de roles y permisos (CU03 y CU04).
/// </summary>
/// <remarks>
/// Reglas de negocio aplicadas aquí (no en el controlador):
/// <list type="bullet">
/// <item>El rol del migrador nunca se administra ni se lista desde la versión web.</item>
/// <item>El rol <c>Administrador</c> está protegido: no se renombra, no se deshabilita y sus
/// permisos no se editan (tiene acceso total implícito).</item>
/// <item>Un rol con usuarios activos asignados no puede deshabilitarse.</item>
/// <item>Los roles no se eliminan: se deshabilitan.</item>
/// <item>Los permisos se guardan como un reemplazo completo e idempotente de la matriz.</item>
/// </list>
/// Los métodos no lanzan excepciones de negocio: devuelven el resultado para que el controlador
/// elija el código HTTP.
/// </remarks>
public interface IRolesService
{
    /// <summary>
    /// Lista los roles administrables (excluye el rol del migrador).
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<IReadOnlyList<RolDto>> ObtenerRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un rol administrable por identificador.
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El rol; <c>null</c> cuando no existe o no es administrable desde la web.</returns>
    Task<RolDto?> ObtenerRolAsync(int idRol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea un rol validando nombre reservado y duplicados.
    /// </summary>
    /// <param name="solicitud">Nombre y descripción.</param>
    /// <param name="auditoria">Contexto de auditoría del usuario autenticado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<RolOperacionDto> CrearRolAsync(
        RolSolicitudDto solicitud,
        ContextoAuditoriaDto auditoria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Modifica nombre y descripción de un rol administrable.
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="solicitud">Nombre y descripción nuevos.</param>
    /// <param name="auditoria">Contexto de auditoría del usuario autenticado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<RolOperacionDto> ActualizarRolAsync(
        int idRol,
        RolSolicitudDto solicitud,
        ContextoAuditoriaDto auditoria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Habilita o deshabilita lógicamente un rol administrable.
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="activo">Estado deseado.</param>
    /// <param name="auditoria">Contexto de auditoría del usuario autenticado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<RolOperacionDto> CambiarEstadoAsync(
        int idRol,
        bool activo,
        ContextoAuditoriaDto auditoria,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el catálogo de permisos (árbol de opciones de menú activas) para pintar la matriz.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<IReadOnlyList<CatalogoPermisoDto>> ObtenerCatalogoPermisosAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la matriz de permisos vigente de un rol.
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La matriz; <c>null</c> cuando el rol no existe o no es administrable desde la web.</returns>
    Task<RolPermisosDto?> ObtenerPermisosAsync(int idRol, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reemplaza la matriz de permisos del rol.
    /// </summary>
    /// <param name="idRol">Identificador del rol.</param>
    /// <param name="solicitud">Matriz completa solicitada.</param>
    /// <param name="auditoria">Contexto de auditoría del usuario autenticado.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<RolPermisosOperacionDto> GuardarPermisosAsync(
        int idRol,
        RolPermisosSolicitudDto solicitud,
        ContextoAuditoriaDto auditoria,
        CancellationToken cancellationToken = default);
}
