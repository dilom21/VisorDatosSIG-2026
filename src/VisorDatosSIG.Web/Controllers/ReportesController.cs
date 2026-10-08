using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Módulo 6: Reportes y Analítica Territorial (CU28 a CU32).
/// </summary>
public class ReportesController : Controller
{
    /// <summary>CU28 – Consultar Dashboard General</summary>
    [HttpGet("/Reportes")]
    [HttpGet("/Reportes/Dashboard")]
    public IActionResult Index() => View();

    /// <summary>CU29 – Consultar Estado de Servicios</summary>
    [HttpGet("/Reportes/Servicios")]
    public IActionResult Servicios() => View();

    /// <summary>CU30 – Consultar Indicadores de Información Geográfica</summary>
    [HttpGet("/Reportes/Indicadores")]
    public IActionResult Indicadores() => View();

    /// <summary>CU31 – Consultar Historial de Migraciones</summary>
    [HttpGet("/Reportes/Migraciones")]
    public IActionResult Migraciones() => View();

    /// <summary>CU32 – Gestionar Reportes</summary>
    [HttpGet("/Reportes/Gestionar")]
    public IActionResult Gestionar() => View();

    private IActionResult Proximamente(string funcionalidad, string ruta) =>
        View("Proximamente", new PaginaProximamenteViewModel
        {
            Modulo = ModulosSistema.Reportes,
            Funcionalidad = funcionalidad,
            RutaActual = ruta
        });
}
