using Microsoft.AspNetCore.Mvc;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Módulo Usuarios y Seguridad: ruta <c>/Bitacora</c> (Consultar Bitácora, CU05).
/// </summary>
/// <remarks>
/// La vista es solo la maqueta del módulo: el navegador consulta la API
/// (<c>GET /api/bitacora</c>, <c>GET /api/bitacora/catalogos</c> y
/// <c>GET /api/bitacora/{id}</c>) con el token de la sesión. La Web nunca se conecta
/// directamente a SQL Server y la pantalla es exclusivamente de lectura: la API aplica los
/// filtros y la paginación en SQL Server y excluye siempre el módulo del migrador.
/// </remarks>
public class BitacoraController : Controller
{
    /// <summary>
    /// Muestra la consulta paginada de la bitácora del sistema.
    /// </summary>
    public IActionResult Index() => View();
}
