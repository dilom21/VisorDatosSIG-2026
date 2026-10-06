using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>Vista MVC de CU17; las operaciones se ejecutan contra la API protegida.</summary>
[Route("Servicios")]
public sealed class ServiciosController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
