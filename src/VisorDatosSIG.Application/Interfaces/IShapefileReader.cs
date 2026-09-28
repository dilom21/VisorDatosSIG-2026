using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato para inspeccionar (solo lectura) un shapefile y sus archivos asociados.
/// La implementación concreta de acceso a archivos vive en la capa de Infrastructure.
/// </summary>
public interface IShapefileReader
{
    /// <summary>
    /// Valida los archivos asociados, detecta la capa, lee la estructura y previsualiza
    /// los primeros registros del shapefile indicado.
    /// </summary>
    /// <param name="shpPath">Ruta del archivo .shp seleccionado.</param>
    /// <param name="cancellationToken">Token para cancelar la operación.</param>
    /// <returns>Resultado de la inspección, incluyendo errores y advertencias.</returns>
    Task<ShapefileInfoDto> InspectAsync(string shpPath, CancellationToken cancellationToken = default);
}
