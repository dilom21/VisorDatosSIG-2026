using System;
using System.Collections.Generic;

namespace VisorDatosSIG.Application.DTOs.Reportes;

/// <summary>
/// KPIs e indicadores consolidados para el Dashboard de Reportes (CU28).
/// </summary>
public sealed class ReportesDashboardDto
{
    public int TotalServicios { get; set; }
    public int ServiciosNormales { get; set; }
    public int ServiciosPendientes { get; set; }
    public int ServiciosParaCorte { get; set; }
    public int ServiciosCortados { get; set; }
    public int ServiciosEnInspeccion { get; set; }

    public int TotalPersonal { get; set; }
    public int PersonalDisponible { get; set; }
    public int PersonalEnServicio { get; set; }

    public int TotalEntidadesSIG { get; set; }
    public int TotalManzanas { get; set; }
    public int TotalLotes { get; set; }
    public int TotalVias { get; set; }
    public int TotalMigracionesEjecutadas { get; set; }

    public List<ComparativaZonaDto> ComparativaZonas { get; set; } = [];
    public List<TendenciaMensualDto> TendenciaTemporal { get; set; } = [];
    public List<DistribucionEstadoDto> DistribucionServicios { get; set; } = [];
}

public sealed class ComparativaZonaDto
{
    public string Zona { get; set; } = string.Empty;
    public int ServiciosProgramados { get; set; }
    public int ServiciosEjecutados { get; set; }
    public int IncidentesReportados { get; set; }
}

public sealed class TendenciaMensualDto
{
    public string Periodo { get; set; } = string.Empty;
    public int Inspecciones { get; set; }
    public int Cortes { get; set; }
    public int Reconexiones { get; set; }
}

public sealed class DistribucionEstadoDto
{
    public int EstadoId { get; set; }
    public string EstadoNombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public double Porcentaje { get; set; }
    public string ColorHex { get; set; } = string.Empty;
}

/// <summary>
/// Reporte de Estado de Servicios (CU29).
/// </summary>
public sealed class EstadoServiciosReporteDto
{
    public int TotalRegistros { get; set; }
    public List<DistribucionEstadoDto> ResumenEstados { get; set; } = [];
    public List<ServicioDetalleDto> Registros { get; set; } = [];
    public int PaginaActual { get; set; }
    public int TotalPaginas { get; set; }
    public int TamanoPagina { get; set; }
}

public sealed class ServicioDetalleDto
{
    public int IdCodigo { get; set; }
    public string? CodF_SQL { get; set; }
    public string? CodF_SIG { get; set; }
    public int? CodFijo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Estado { get; set; }
    public string EstadoNombre { get; set; } = string.Empty;
    public int? IdLote { get; set; }
    public double? Longitud { get; set; }
    public double? Latitud { get; set; }
}

/// <summary>
/// Reporte de Indicadores Geográficos (CU30).
/// </summary>
public sealed class IndicadoresGeograficosDto
{
    public int TotalEntidades { get; set; }
    public List<CapaMetricaDto> Capas { get; set; } = [];
    public CoberturaTerritorialDto Cobertura { get; set; } = new();
    public DensidadesCalculadasDto Densidades { get; set; } = new();
}

public sealed class CapaMetricaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string TipoGeometria { get; set; } = string.Empty;
    public int TotalRegistros { get; set; }
    public double PorcentajeDelTotal { get; set; }
}

public sealed class CoberturaTerritorialDto
{
    public string SistemaReferencia { get; set; } = "WGS 84 (EPSG:4326)";
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public string Municipio { get; set; } = "Santa Cruz de la Sierra";
    public string Departamento { get; set; } = "Santa Cruz, Bolivia";
}

public sealed class DensidadesCalculadasDto
{
    public double PromedioLotesPorManzana { get; set; }
    public double PromedioCodigosPorLote { get; set; }
    public int TotalManzanasConLotes { get; set; }
    public double KilometrosViasEstimados { get; set; }
}

/// <summary>
/// Reporte de Historial de Migraciones (CU31).
/// </summary>
public sealed class HistorialMigracionesDto
{
    public int TotalMigraciones { get; set; }
    public int TotalRegistrosMigrados { get; set; }
    public int MigracionesExitosas { get; set; }
    public int MigracionesConAdvertencia { get; set; }
    public List<MigracionItemDto> Eventos { get; set; } = [];
}

public sealed class MigracionItemDto
{
    public long IdBitacora { get; set; }
    public DateTime FechaHoraBolivia { get; set; }
    public string Archivo { get; set; } = string.Empty;
    public string Capa { get; set; } = string.Empty;
    public string Modalidad { get; set; } = string.Empty;
    public int RegistrosOrigen { get; set; }
    public int RegistrosProcesados { get; set; }
    public int RegistrosInsertados { get; set; }
    public int RegistrosOmitidos { get; set; }
    public int RegistrosFallidos { get; set; }
    public double DuracionSegundos { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
}

/// <summary>
/// Solicitud de Generación y Exportación de Reportes (CU32).
/// </summary>
public sealed class SolicitudExportacionDto
{
    public string TipoReporte { get; set; } = "EstadoServicios"; // Dashboard, EstadoServicios, IndicadoresGeograficos, HistorialMigraciones
    public string Formato { get; set; } = "pdf"; // pdf, xlsx, csv, txt
    public int? EstadoFiltro { get; set; }
    public string? Busqueda { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? GraficoBase64 { get; set; }
    public string? Titulo { get; set; }
}

/// <summary>
/// Archivo exportado devuelto para descarga binaria (CU32).
/// </summary>
public sealed class ArchivoExportadoDto
{
    public string NombreArchivo { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Contenido { get; set; } = [];
}

/// <summary>
/// Vista previa estructurada con metadatos y filas tabulares antes de exportar (CU32).
/// </summary>
public sealed class ReporteVistaPreviaDto
{
    public string TipoReporte { get; set; } = string.Empty;
    public string CodigoReporte { get; set; } = string.Empty;
    public string Codigo { get => CodigoReporte; set => CodigoReporte = value; }
    public string Titulo { get; set; } = string.Empty;
    public string Subtitulo { get; set; } = string.Empty;
    public string Modulo { get; set; } = string.Empty;
    public int TotalRegistros { get; set; }
    public List<string> Columnas { get; set; } = [];
    public List<List<string>> Filas { get; set; } = [];
    public DateTime FechaGeneracionBolivia { get; set; }
    public DateTime FechaHoraBolivia { get => FechaGeneracionBolivia; set => FechaGeneracionBolivia = value; }
    public string Operador { get; set; } = string.Empty;
    public Dictionary<string, string> Metadatos { get; set; } = [];
}

