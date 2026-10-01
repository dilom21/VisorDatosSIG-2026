namespace VisorDatosSIG.Application.DTOs.Security;

/// <summary>
/// Rol asignado a un usuario, tal como se lee de <c>dbo.UsuariosRoles</c>.
/// </summary>
/// <remarks>
/// Modelo interno del backend para resolver la autorización: se devuelve el identificador y el
/// nombre del rol activo. Las reglas sobre nombres reservados (<c>Administrador</c>) y la
/// exclusión del rol de migración se aplican en <c>RolesSistema</c>, no en el repositorio.
/// </remarks>
public sealed class RolAsignadoDto
{
    /// <summary>Identificador del rol activo.</summary>
    public int IdRol { get; init; }

    /// <summary>Nombre del rol activo.</summary>
    public string NombreRol { get; init; } = string.Empty;
}

/// <summary>
/// Permisos efectivos de un usuario para la aplicación web, calculados a partir de sus roles.
/// </summary>
/// <remarks>
/// La unión de roles es acumulativa: si cualquiera de los roles activos del usuario otorga el
/// permiso, el usuario lo tiene. El rol <c>Administrador</c> otorga acceso total implícito y el
/// rol del migrador se ignora por completo en la seguridad web.
/// </remarks>
public sealed class PermisosEfectivosDto
{
    /// <summary>
    /// Indica si el usuario puede usar la aplicación web (tiene al menos un rol web activo).
    /// Un usuario que solo tenga el rol del migrador no puede acceder al visor web.
    /// </summary>
    public bool TieneAccesoWeb { get; init; }

    /// <summary>
    /// Indica que el usuario es <c>Administrador</c> y, por lo tanto, ve todas las opciones de
    /// menú y puede ejecutar cualquier acción sin depender de <c>dbo.RolMenu</c>.
    /// </summary>
    public bool AccesoTotal { get; init; }

    /// <summary>
    /// Identificadores de los menús que el usuario puede ver. Está vacío cuando
    /// <see cref="AccesoTotal"/> es <c>true</c>, porque en ese caso no hay restricción.
    /// </summary>
    public IReadOnlySet<int> MenusVisibles { get; init; } = new HashSet<int>();
}
