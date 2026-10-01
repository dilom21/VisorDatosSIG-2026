using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Módulo Usuarios y Seguridad: ruta <c>/Usuarios</c> (Gestionar Usuarios, CU03).
/// </summary>
/// <remarks>
/// La vista es la maqueta del módulo: el listado, las altas, la edición, el cambio de estado y
/// el restablecimiento de contraseña los ejecuta el navegador contra la API
/// (<c>/api/usuarios</c>) con el token de la sesión. La Web nunca se conecta directamente a
/// SQL Server.
/// <para>
/// La autorización real vive en la API, que resuelve en cada petición el permiso dinámico sobre
/// la opción de menú <c>/Usuarios</c> de <c>dbo.RolMenu</c> (ver, crear y editar); el rol
/// <c>Administrador</c> mantiene el acceso total implícito. El sidebar dinámico sigue siendo el
/// único origen de navegación hacia este módulo: no hay enlaces fijos al módulo en el header.
/// </para>
/// </remarks>
public class UsuariosController : Controller
{
    /// <summary>
    /// Muestra la pantalla de gestión de usuarios del workspace autenticado.
    /// </summary>
    /// <returns>La vista del módulo; el contenido lo publica el módulo JS tras validar la sesión.</returns>
    public IActionResult Index() => View();
}
