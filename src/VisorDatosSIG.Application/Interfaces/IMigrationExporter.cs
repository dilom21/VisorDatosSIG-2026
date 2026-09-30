using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato para la exportación de resúmenes de migración e informes técnicos (RF-MIG-13).
/// </summary>
public interface IMigrationExporter
{
    /// <summary>
    /// Exporta un informe ejecutivo comprensible a texto (.txt / .md).
    /// </summary>
    Task ExportarTextoAsync(
        string rutaArchivo,
        ShapefileInfoDto? inspeccion,
        ShapefileValidationResultDto? validacion,
        MigrationResult? migracion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exporta la lista detallada de incidencias y registros analizados a CSV.
    /// </summary>
    Task ExportarCsvAsync(
        string rutaArchivo,
        ShapefileInfoDto? inspeccion,
        ShapefileValidationResultDto? validacion,
        MigrationResult? migracion,
        CancellationToken cancellationToken = default);
}
