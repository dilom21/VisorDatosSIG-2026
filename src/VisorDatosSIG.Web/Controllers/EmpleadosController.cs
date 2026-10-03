using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Módulo Seguimiento de Servicios / Operaciones: ruta <c>/Empleados</c> (CU04 – Gestionar Empleados).
/// </summary>
/// <remarks>
/// La vista representa la interfaz de administración operativa: el listado de personal, altas,
/// modificaciones, cambios de estado y gestión de disponibilidad operativa los ejecuta el navegador
/// contra la API (<c>/api/empleados</c>) utilizando el token JWT de la sesión.
/// </remarks>
public class EmpleadosController : Controller
{
    /// <summary>
    /// Muestra la pantalla principal de gestión de empleados.
    /// </summary>
    public IActionResult Index() => View();
}
