using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

public class VisorController : Controller
{
    public IActionResult Index() => View();
}
