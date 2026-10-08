using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>Vista MVC de CU17; las operaciones se ejecutan contra la API protegida.</summary>
[Route("Servicios")]
public sealed class ServiciosController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();

    /// <summary>Ruta <c>/Servicios/Disponibilidad</c> (CU19): disponibilidad, horarios y asignaciones.</summary>
    /// <remarks>
    /// La vista es la maqueta del módulo: la disponibilidad, los horarios y las asignaciones los
    /// ejecuta el navegador contra la API protegida con el token de la sesión
    /// (ver <c>js/servicios/disponibilidad.js</c>):
    /// <c>/api/disponibilidad</c> y <c>/api/asignaciones-trabajo</c>. La Web nunca se conecta a SQL
    /// Server. La autorización real vive en la API, que resuelve en cada petición el permiso
    /// dinámico sobre la opción de menú <c>/Servicios/Disponibilidad</c> de <c>dbo.RolMenu</c>.
    /// </remarks>
    [HttpGet("Disponibilidad")]
    public IActionResult Disponibilidad() => View();
}
