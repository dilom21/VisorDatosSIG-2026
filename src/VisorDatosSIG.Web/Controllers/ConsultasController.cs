using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Consultas y Filtros (CU13 al CU16). Las vistas consumen la API protegida.
/// </summary>
/// <remarks>Las rutas coinciden con dbo.MenuOpciones; los permisos se validan en la API.</remarks>
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

    /// <summary>CU15: consultar lotes.</summary>
    public IActionResult Lotes() => View();

    /// <summary>CU16: consultar v?as.</summary>
    public IActionResult Vias() => View();
}
