using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Módulo Consultas y Filtros (CU12 al CU15). Cada acción reserva su ruta y muestra la
/// página "Próximamente" para que las opciones del menú dinámico no lleven a un 404.
/// </summary>
/// <remarks>
/// No se reutiliza el visor cartográfico ni se duplican sus consultas: cuando estas
/// funcionalidades se implementen, reemplazarán únicamente el cuerpo de cada acción.
/// Las rutas coinciden exactamente con la columna <c>Url</c> de <c>dbo.MenuOpciones</c>.
/// </remarks>
public class ConsultasController : Controller
{
    /// <summary>Ruta <c>/Consultas/CodigoFijo</c> (CU12).</summary>
    public IActionResult CodigoFijo() => Proximamente("Consultar Código Fijo", "/Consultas/CodigoFijo");

    /// <summary>Ruta <c>/Consultas/Manzana</c> (CU13).</summary>
    public IActionResult Manzana() => Proximamente("Consultar Manzana", "/Consultas/Manzana");

    /// <summary>Ruta <c>/Consultas/Lotes</c> (CU14).</summary>
    public IActionResult Lotes() => Proximamente("Consultar Lotes", "/Consultas/Lotes");

    /// <summary>Ruta <c>/Consultas/Vias</c> (CU15).</summary>
    public IActionResult Vias() => Proximamente("Consultar Vías", "/Consultas/Vias");

    private IActionResult Proximamente(string funcionalidad, string ruta) =>
        View("Proximamente", new PaginaProximamenteViewModel
        {
            Modulo = ModulosSistema.ConsultasYFiltros,
            Funcionalidad = funcionalidad,
            RutaActual = ruta
        });
}
