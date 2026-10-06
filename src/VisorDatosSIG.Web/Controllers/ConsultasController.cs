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
    /// <summary>Ruta <c>/Consultas/CodigoFijo</c> (CU13): consultar código fijo.</summary>
    /// <remarks>
    /// La vista es la maqueta del módulo: la búsqueda paginada y el detalle los ejecuta el
    /// navegador contra la API protegida (<c>/api/codigos-fijos</c>) con el token de la sesión
    /// (ver <c>js/consultas/codigo-fijo.js</c>). La Web nunca se conecta directamente a SQL
    /// Server. La autorización real vive en la API, que resuelve en cada petición el permiso
    /// dinámico sobre la opción de menú <c>/Consultas/CodigoFijo</c> de <c>dbo.RolMenu</c>.
    /// </remarks>
    public IActionResult CodigoFijo() => View();

    /// <summary>Ruta <c>/Consultas/Manzana</c> (CU14): consultar manzana.</summary>
    /// <remarks>
    /// La vista es la maqueta del módulo: la búsqueda paginada y el detalle los ejecuta el
    /// navegador contra la API protegida (<c>/api/manzanas</c>) con el token de la sesión
    /// (ver <c>js/consultas/manzana.js</c>). La Web nunca se conecta directamente a SQL Server.
    /// La autorización real vive en la API, que resuelve en cada petición el permiso dinámico
    /// sobre la opción de menú <c>/Consultas/Manzana</c> de <c>dbo.RolMenu</c>.
    /// </remarks>
    public IActionResult Manzana() => View();

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
