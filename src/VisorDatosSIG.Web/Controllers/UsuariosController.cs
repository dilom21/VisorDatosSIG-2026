using Microsoft.AspNetCore.Mvc;
namespace VisorDatosSIG.Web.Controllers;
// Las credenciales Bearer y la autorización se verifican en la API.
public class UsuariosController : Controller
{
    public IActionResult Index() => View();
}
