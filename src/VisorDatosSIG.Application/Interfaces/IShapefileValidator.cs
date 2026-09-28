using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato para la validación detallada (registro por registro) de un shapefile
/// antes de cualquier migración a SQL Server.
/// La implementación concreta con NetTopologySuite vive en Infrastructure.
/// </summary>
public interface IShapefileValidator
{
    /// <summary>
    /// Analiza todos los registros del shapefile, aplica las reglas comunes y las
    /// reglas específicas de la capa, y devuelve el resumen, las estadísticas y las incidencias.
    /// </summary>
    /// <param name="shpPath">Ruta del archivo .shp previamente inspeccionado.</param>
    /// <param name="layer">Capa detectada en la inspección previa.</param>
    /// <param name="progress">Receptor opcional de progreso.</param>
    /// <param name="cancellationToken">Token para cancelar la validación.</param>
    /// <returns>Resultado de la validación (no modifica ningún archivo).</returns>
    Task<ShapefileValidationResultDto> ValidateAsync(
        string shpPath,
        ShapefileLayer layer,
        IProgress<ValidationProgressDto>? progress = null,
        CancellationToken cancellationToken = default);
}
