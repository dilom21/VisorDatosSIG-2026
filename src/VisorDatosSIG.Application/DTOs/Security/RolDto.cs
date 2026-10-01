using VisorDatosSIG.Application.Common;

namespace VisorDatosSIG.Application.DTOs.Security;

/// <summary>
/// Rol del sistema tal como se expone en <c>GET /api/roles</c> (CU03 - Gestionar Roles).
/// </summary>
/// <remarks>
/// No expone el identificador de ningún usuario: solo el nombre del rol, su estado lógico y
/// cuántos usuarios activos lo tienen asignado, dato que administración necesita para saber
/// si el rol puede deshabilitarse.
/// </remarks>
public sealed class RolDto
{
    /// <summary>Identificador del rol.</summary>
    public int IdRol { get; init; }

    /// <summary>Nombre del rol, único en el sistema.</summary>
    public string NombreRol { get; init; } = string.Empty;

    /// <summary>Descripción funcional del rol; <c>null</c> cuando no se registró.</summary>
    public string? Descripcion { get; init; }

    /// <summary>Estado lógico del rol: <c>true</c> habilitado, <c>false</c> deshabilitado.</summary>
    public bool Activo { get; init; }

    /// <summary>
    /// Indica que el rol pertenece al sistema y está protegido (por ejemplo
    /// <c>Administrador</c>): no puede renombrarse, deshabilitarse ni editarle permisos.
    /// </summary>
    public bool EsRolSistema { get; init; }

    /// <summary>Cantidad de usuarios activos que tienen asignado el rol.</summary>
    public int CantidadUsuariosActivos { get; init; }
}

/// <summary>
/// Fila de <c>dbo.Roles</c> con el conteo de usuarios activos asignados.
/// </summary>
/// <remarks>
/// Es un modelo de lectura interno del backend: el repositorio lo entrega con la forma de la
/// base de datos y el servicio lo convierte en <see cref="RolDto"/> agregando las reglas de
/// negocio (roles del sistema, exclusión del migrador).
/// </remarks>
public sealed class RolFilaDto
{
    /// <summary>Identificador del rol.</summary>
    public int IdRol { get; init; }

    /// <summary>Nombre del rol tal como está almacenado.</summary>
    public string NombreRol { get; init; } = string.Empty;

    /// <summary>Descripción del rol; <c>null</c> cuando no se registró.</summary>
    public string? Descripcion { get; init; }

    /// <summary>Estado lógico almacenado en <c>dbo.Roles.Estado</c>.</summary>
    public bool Activo { get; init; }

    /// <summary>Cantidad de usuarios activos (<c>dbo.Usuarios.Activo = 1</c>) con el rol asignado.</summary>
    public int CantidadUsuariosActivos { get; init; }

    /// <summary>
    /// Proyecta la fila al contrato público marcando los roles del sistema.
    /// </summary>
    public RolDto AContrato() => new()
    {
        IdRol = IdRol,
        NombreRol = NombreRol,
        Descripcion = Descripcion,
        Activo = Activo,
        EsRolSistema = RolesSistema.EsAdministrador(NombreRol),
        CantidadUsuariosActivos = CantidadUsuariosActivos
    };
}
