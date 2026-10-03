namespace VisorDatosSIG.Application.Common;

/// <summary>
/// Acción que se desea ejecutar sobre una opción de menú y que se resuelve contra las
/// columnas de permiso de <c>dbo.RolMenu</c>.
/// </summary>
/// <remarks>
/// El mapeo verbo HTTP → permiso es: GET → <see cref="Ver"/>, POST → <see cref="Crear"/>,
/// PATCH/PUT → <see cref="Editar"/> y la deshabilitación lógica (eliminación lógica) →
/// <see cref="Eliminar"/>.
/// </remarks>
public enum AccionPermiso
{
    /// <summary>Consultar (<c>RolMenu.PuedeVer</c>).</summary>
    Ver = 1,

    /// <summary>Registrar (<c>RolMenu.PuedeCrear</c>).</summary>
    Crear = 2,

    /// <summary>Modificar (<c>RolMenu.PuedeEditar</c>).</summary>
    Editar = 3,

    /// <summary>Eliminar o deshabilitar (<c>RolMenu.PuedeEliminar</c>).</summary>
    Eliminar = 4
}

/// <summary>
/// Rutas estables de las opciones de menú utilizadas para autorizar los endpoints web.
/// </summary>
/// <remarks>
/// La autorización se resuelve por la <c>Url</c> de <c>dbo.MenuOpciones</c>, nunca por
/// <c>IdMenu</c>: los identificadores son <c>IDENTITY</c>. Las rutas declaradas aquí son las
/// mismas que siembra <c>database/scripts/06_Menu_Navegacion.sql</c> y las que consumen los
/// controladores de VisorDatosSIG.Web.
/// </remarks>
public static class PermisoMenu
{
    /// <summary>Módulo: gestión de usuarios (CU02).</summary>
    public const string Usuarios = "/Usuarios";

    /// <summary>Módulo: gestión de roles y permisos (CU03/CU04).</summary>
    public const string Roles = "/Roles";

    /// <summary>Módulo: consulta de bitácora (CU05).</summary>
    public const string Bitacora = "/Bitacora";

    /// <summary>Módulo: visor cartográfico.</summary>
    public const string Visor = "/Visor";

    /// <summary>Consulta de código fijo.</summary>
    public const string ConsultaCodigoFijo = "/Consultas/CodigoFijo";

    /// <summary>Consulta por manzana.</summary>
    public const string ConsultaManzana = "/Consultas/Manzana";

    /// <summary>Consulta de lotes.</summary>
    public const string ConsultaLotes = "/Consultas/Lotes";

    /// <summary>Consulta de vías.</summary>
    public const string ConsultaVias = "/Consultas/Vias";

    /// <summary>Módulo: reportes.</summary>
    public const string Reportes = "/Reportes";

    /// <summary>Módulo: gestión de empleados (CU04 - Seguimiento de Servicios).</summary>
    public const string Empleados = "/Empleados";

    /// <summary>
    /// Todas las rutas de menú del sistema web.
    /// </summary>
    public static readonly IReadOnlyList<string> Todas =
    [
        Usuarios,
        Roles,
        Bitacora,
        Visor,
        ConsultaCodigoFijo,
        ConsultaManzana,
        ConsultaLotes,
        ConsultaVias,
        Reportes,
        Empleados
    ];

    /// <summary>
    /// Normaliza una ruta de menú para compararla: recorta espacios y quita la barra final.
    /// </summary>
    /// <param name="url">Ruta almacenada en <c>dbo.MenuOpciones.Url</c>.</param>
    /// <returns>Ruta normalizada; <c>null</c> cuando el valor es nulo o vacío.</returns>
    public static string? NormalizarUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var recortada = url.Trim();

        return recortada.Length > 1 ? recortada.TrimEnd('/') : recortada;
    }
}
