using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Controlador de la vista principal del Dashboard para el administrador y supervisores.
/// </summary>
public class DashboardController : Controller
{
    public IActionResult Index() => View();
}
