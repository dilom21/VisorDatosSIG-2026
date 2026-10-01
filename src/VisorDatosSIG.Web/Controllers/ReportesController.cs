using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Módulo Reportes: ruta <c>/Reportes</c> (Gestionar Reportes, CU22).
/// </summary>
/// <remarks>
/// Pendiente de implementación: se muestra la página "Próximamente" para no romper la
/// navegación del sidebar ni llevar al usuario a un 404.
/// </remarks>
public class ReportesController : Controller
{
    /// <summary>Ruta <c>/Reportes</c>.</summary>
    public IActionResult Index() => View("Proximamente", new PaginaProximamenteViewModel
    {
        Modulo = ModulosSistema.Reportes,
        Funcionalidad = "Gestionar Reportes",
        RutaActual = "/Reportes"
    });
}
