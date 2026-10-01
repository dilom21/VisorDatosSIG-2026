using Microsoft.AspNetCore.Mvc;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Web.Models;

namespace VisorDatosSIG.Web.Controllers;

/// <summary>
/// Módulo Usuarios y Seguridad: ruta <c>/Usuarios</c> (Gestionar Usuarios, CU01).
/// </summary>
/// <remarks>
/// La funcionalidad todavía no está implementada. La ruta existe para que la opción
/// del menú dinámico no termine en un 404 y muestra la página "Próximamente" dentro
/// del layout autenticado. No se inventa ningún CRUD.
/// </remarks>
public class UsuariosController : Controller
{
    public IActionResult Index() => View("Proximamente", new PaginaProximamenteViewModel
    {
        Modulo = ModulosSistema.UsuariosYSeguridad,
        Funcionalidad = "Gestionar Usuarios",
        RutaActual = "/Usuarios"
    });
}
