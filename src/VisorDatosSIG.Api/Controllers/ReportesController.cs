using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VisorDatosSIG.Application.DTOs.Reportes;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Authentication;

namespace VisorDatosSIG.Api.Controllers;

/// <summary>
/// Controlador de la API para el Módulo 6 – Reportes y Estadísticas (CU28 al CU32).
/// Cumple con las reglas funcionales de autorización en servidor RN-REP-01, RN-REP-02, RN-REP-03 y RN-REP-12.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public sealed class ReportesController : ControllerBase
{
    private readonly IReportesService _reportesService;
    private readonly ILogger<ReportesController> _logger;

    public ReportesController(
        IReportesService reportesService,
        ILogger<ReportesController> logger)
    {
        _reportesService = reportesService ?? throw new ArgumentNullException(nameof(reportesService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private string ObtenerLoginActual()
    {
        return User.FindFirstValue(JwtTokenService.ClaimLogin)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.Identity?.Name
            ?? "Usuario";
    }

    /// <summary>
    /// CU28: Consulta indicadores consolidados para el Dashboard de Reportes.
    /// Permitido para: Administrador y Supervisor (RN-REP-02).
    /// </summary>
    [HttpGet("dashboard")]
    [Authorize(Roles = "Administrador,Supervisor")]
    [ProducesResponseType(typeof(ReportesDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerDashboard(CancellationToken cancellationToken)
    {
        var resultado = await _reportesService.ObtenerDashboardAsync(cancellationToken);
        return Ok(resultado);
    }

    /// <summary>
    /// CU29: Consulta el reporte de estado de servicios con filtros y paginación.
    /// Permitido para: Administrador y Supervisor (RN-REP-02, RN-REP-07, RN-REP-11).
    /// </summary>
    [HttpGet("servicios")]
    [Authorize(Roles = "Administrador,Supervisor")]
    [ProducesResponseType(typeof(EstadoServiciosReporteDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerEstadoServicios(
        [FromQuery] int? estado,
        [FromQuery] string? busqueda,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 25,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _reportesService.ObtenerEstadoServiciosAsync(estado, busqueda, pagina, tamano, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>
    /// CU30: Consulta los indicadores y métricas de las capas geográficas del sistema.
    /// Permitido para: Administrador y Supervisor (RN-REP-02, RN-REP-08).
    /// </summary>
    [HttpGet("indicadores-geograficos")]
    [Authorize(Roles = "Administrador,Supervisor")]
    [ProducesResponseType(typeof(IndicadoresGeograficosDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerIndicadoresGeograficos(CancellationToken cancellationToken)
    {
        var resultado = await _reportesService.ObtenerIndicadoresGeograficosAsync(cancellationToken);
        return Ok(resultado);
    }

    /// <summary>
    /// CU31: Consulta el historial de procesos de migración espacial ejecutados.
    /// Permitido EXCLUSIVAMENTE para: Administrador y Responsable de Migración (RN-REP-03).
    /// </summary>
    [HttpGet("migraciones")]
    [Authorize(Roles = "Administrador,Responsable de Migración")]
    [ProducesResponseType(typeof(HistorialMigracionesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerHistorialMigraciones(
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _reportesService.ObtenerHistorialMigracionesAsync(limite, cancellationToken);
        return Ok(resultado);
    }

    /// <summary>
    /// CU32: Genera y exporta el reporte seleccionado en PDF, Excel (.xlsx), CSV o TXT.
    /// Cumple con la validación de autorización según el tipo de reporte solicitado (RN-REP-12).
    /// </summary>
    [HttpPost("exportar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportarReporte(
        [FromBody] SolicitudExportacionDto solicitud,
        CancellationToken cancellationToken)
    {
        if (solicitud is null)
        {
            return BadRequest(new { mensaje = "La solicitud de exportación es obligatoria." });
        }

        // RN-REP-12: Si se solicita reporte de migraciones, verificar rol autorizado
        if (solicitud.TipoReporte.Equals("HistorialMigraciones", StringComparison.OrdinalIgnoreCase))
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Responsable de Migración"))
            {
                return Forbid();
            }
        }
        else
        {
            if (!User.IsInRole("Administrador") && !User.IsInRole("Supervisor"))
            {
                return Forbid();
            }
        }

        var loginUsuario = ObtenerLoginActual();
        var archivo = await _reportesService.ExportarReporteAsync(solicitud, loginUsuario, cancellationToken);

        return File(archivo.Contenido, archivo.ContentType, archivo.NombreArchivo);
    }
}
