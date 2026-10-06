using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Pantalla web de CU18. Las operaciones se ejecutan en el navegador contra la API.
/// </summary>
[Route("Servicios/BajasParciales")]
public sealed class BajasParcialesController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
