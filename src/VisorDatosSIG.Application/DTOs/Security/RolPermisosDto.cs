using VisorDatosSIG.Application.Common;

namespace VisorDatosSIG.Application.DTOs.Security;

/// <summary>
/// Opción del catálogo de permisos, con sus opciones hijas anidadas.
/// </summary>
/// <remarks>
/// Contrato de <c>GET /api/roles/catalogo-permisos</c>. Se arma con las opciones activas de
/// <c>dbo.MenuOpciones</c> y refleja exactamente la jerarquía del menú lateral, de modo que el
/// módulo de administración no necesita nombres ni rutas hardcodeadas.
/// </remarks>
public sealed class CatalogoPermisoDto
{
    /// <summary>Identificador de la opción de menú.</summary>
    public int IdMenu { get; init; }

    /// <summary>Nombre visible de la opción.</summary>
    public string NombreMenu { get; init; } = string.Empty;

    /// <summary>Ruta navegable; <c>null</c> cuando la opción es un agrupador.</summary>
    public string? Url { get; init; }

    /// <summary>Clave del icono del catálogo del frontend.</summary>
    public string? Icono { get; init; }

    /// <summary>Orden de presentación dentro de su nivel.</summary>
    public int Orden { get; init; }

    /// <summary>Opciones hijas ordenadas; colección vacía cuando no tiene hijas.</summary>
    public IReadOnlyList<CatalogoPermisoDto> Opciones { get; init; } = [];
}

/// <summary>
/// Fila de la matriz de permisos: un menú con los cuatro permisos otorgados al rol.
/// </summary>
/// <remarks>
/// La matriz se expone plana (una fila por menú, ordenada igual que el catálogo e incluyendo
/// <see cref="IdMenuPadre"/>) porque es la forma natural de una tabla de casillas y permite que
/// <c>GET</c> y <c>PUT</c> de <c>/api/roles/{id}/permisos</c> compartan exactamente la misma
/// estructura.
/// </remarks>
public sealed class MenuPermisoDto
{
    /// <summary>Identificador de la opción de menú.</summary>
    public int IdMenu { get; init; }

    /// <summary>Identificador del menú padre; <c>null</c> en las opciones de primer nivel.</summary>
    public int? IdMenuPadre { get; init; }

    /// <summary>Nombre visible de la opción.</summary>
    public string NombreMenu { get; init; } = string.Empty;

    /// <summary>Ruta de la opción; <c>null</c> cuando es un agrupador.</summary>
    public string? Url { get; init; }

    /// <summary>Clave del icono del catálogo del frontend.</summary>
    public string? Icono { get; init; }

    /// <summary>Orden de presentación dentro de su nivel.</summary>
    public int Orden { get; init; }

    /// <summary>
    /// Indica que la opción no navega a ninguna pantalla (<see cref="Url"/> nulo): solo agrupa
    /// opciones hijas y, por lo tanto, no autoriza ninguna acción de la API.
    /// </summary>
    public bool EsAgrupador { get; init; }

    /// <summary>Permite ver la opción en el menú lateral.</summary>
    public bool PuedeVer { get; init; }

    /// <summary>Permite registrar registros en el módulo.</summary>
    public bool PuedeCrear { get; init; }

    /// <summary>Permite modificar registros del módulo.</summary>
    public bool PuedeEditar { get; init; }

    /// <summary>Permite eliminar o deshabilitar registros del módulo.</summary>
    public bool PuedeEliminar { get; init; }
}

/// <summary>
/// Matriz de permisos vigente de un rol (CU04 - Gestionar Permisos).
/// </summary>
/// <remarks>
/// Contrato de <c>GET /api/roles/{id}/permisos</c> y respuesta de
/// <c>PUT /api/roles/{id}/permisos</c>. La ausencia de una fila equivale a "sin permisos":
/// el sistema nunca otorga permisos implícitos, salvo el acceso total del rol
/// <c>Administrador</c>.
/// </remarks>
public sealed class RolPermisosDto
{
    /// <summary>Identificador del rol.</summary>
    public int IdRol { get; init; }

    /// <summary>Nombre del rol.</summary>
    public string NombreRol { get; init; } = string.Empty;

    /// <summary>Indica que el rol es del sistema y sus permisos son implícitos (no editables).</summary>
    public bool EsRolSistema { get; init; }

    /// <summary>Filas de la matriz, una por opción de menú activa.</summary>
    public IReadOnlyList<MenuPermisoDto> Menus { get; init; } = [];
}

/// <summary>
/// Fila de <c>dbo.RolMenu</c> correspondiente a un menú: es la unidad de escritura y de
/// autorización del sistema de permisos.
/// </summary>
/// <remarks>
/// Modelo interno del backend. La ausencia de fila para un menú significa "sin permiso"; el
/// repositorio siempre devuelve filas con <c>IdMenu</c> válido.
/// </remarks>
public sealed class PermisoMenuFilaDto
{
    /// <summary>Identificador de la opción de menú.</summary>
    public int IdMenu { get; init; }

    /// <summary>Permite ver la opción.</summary>
    public bool PuedeVer { get; init; }

    /// <summary>Permite crear.</summary>
    public bool PuedeCrear { get; init; }

    /// <summary>Permite editar.</summary>
    public bool PuedeEditar { get; init; }

    /// <summary>Permite eliminar.</summary>
    public bool PuedeEliminar { get; init; }

    /// <summary>Indica si la fila otorga al menos un permiso.</summary>
    public bool TieneAlgunPermiso => PuedeVer || PuedeCrear || PuedeEditar || PuedeEliminar;

    /// <summary>
    /// Indica si la fila otorga la acción indicada.
    /// </summary>
    /// <param name="accion">Acción a evaluar.</param>
    public bool Otorga(AccionPermiso accion) => accion switch
    {
        AccionPermiso.Ver => PuedeVer,
        AccionPermiso.Crear => PuedeCrear,
        AccionPermiso.Editar => PuedeEditar,
        AccionPermiso.Eliminar => PuedeEliminar,
        _ => false
    };
}
