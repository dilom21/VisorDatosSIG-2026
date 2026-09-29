using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Controlador de la vista de inicio de sesión.
/// La Web no valida credenciales: solo entrega el formulario y el navegador
/// consume directamente la API de autenticación. La normalización de
/// <c>returnUrl</c> evita un open redirect al aceptar únicamente rutas locales.
/// </summary>
public class CuentaController : Controller
{
    public IActionResult Login(string? returnUrl, string? motivo)
    {
        ViewData["ReturnUrl"] = NormalizarReturnUrl(returnUrl);
        ViewData["Motivo"] = motivo is "expirada" or "sesion-finalizada" ? motivo : null;
        return View();
    }

    private static string NormalizarReturnUrl(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return "/Visor";
        }

        var recortado = valor.Trim();

        if (recortado[0] != '/'
            || recortado.StartsWith("//", StringComparison.Ordinal)
            || recortado.StartsWith("/\\", StringComparison.Ordinal)
            || recortado.Contains('\\')
            || recortado.Contains("://", StringComparison.Ordinal))
        {
            return "/Visor";
        }

        return recortado;
    }
}
