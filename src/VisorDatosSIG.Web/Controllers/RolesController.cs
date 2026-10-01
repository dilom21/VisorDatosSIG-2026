using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Módulo Usuarios y Seguridad: ruta <c>/Roles</c> (Gestionar Roles y Permisos, CU03 y CU04).
/// </summary>
/// <remarks>
/// La vista es solo la maqueta del módulo: el listado de roles, la matriz de permisos y las
/// operaciones de alta, edición y cambio de estado las ejecuta el navegador contra la API
/// (<c>/api/roles</c> y <c>/api/roles/{id}/permisos</c>) con el token de la sesión. La Web
/// nunca se conecta directamente a SQL Server y la autorización real vive en la API
/// (permiso de ver, crear y editar sobre la opción de menú <c>/Roles</c>).
/// </remarks>
public class RolesController : Controller
{
    /// <summary>
    /// Muestra la pantalla de administración de roles y permisos.
    /// </summary>
    public IActionResult Index() => View();
}
