using System;
using System.Threading;
using System.Threading.Tasks;
using VisorDatosSIG.Application.DTOs.Reportes;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato de servicio para el Módulo 6 – Reportes (CU28 a CU32).
/// Cumple con las reglas funcionales RN-REP-01 a RN-REP-14.
/// </summary>
public interface IReportesService
{
    /// <summary>
    /// CU28: Consulta indicadores consolidados para el Dashboard de Reportes.
    /// </summary>
    Task<ReportesDashboardDto> ObtenerDashboardAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// CU29: Consulta el reporte detallado y paginado del Estado de Servicios.
    /// </summary>
    Task<EstadoServiciosReporteDto> ObtenerEstadoServiciosAsync(
        int? estado,
        string? busqueda,
        int pagina,
        int tamano,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// CU30: Consulta las métricas e indicadores de las capas geográficas del sistema.
    /// </summary>
    Task<IndicadoresGeograficosDto> ObtenerIndicadoresGeograficosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// CU31: Consulta el historial de procesos de migración espacial ejecutados.
    /// </summary>
    Task<HistorialMigracionesDto> ObtenerHistorialMigracionesAsync(int limite = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// CU32: Genera y exporta el reporte solicitado en PDF, Excel (XLSX), CSV o TXT.
    /// </summary>
    Task<ArchivoExportadoDto> ExportarReporteAsync(
        SolicitudExportacionDto solicitud,
        string loginUsuario,
        CancellationToken cancellationToken = default);
}
