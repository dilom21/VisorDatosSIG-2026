using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato para determinar a cuál de las capas oficiales corresponde un archivo shapefile.
/// La detección está centralizada en una única implementación (Infrastructure).
/// </summary>
public interface IShapefileLayerDetector
{
    /// <summary>
    /// Detecta la capa oficial a partir del nombre del archivo shapefile.
    /// </summary>
    /// <param name="shpFileName">Nombre del archivo (con o sin extensión). No puede ser nulo.</param>
    /// <returns>Resultado de la detección con la capa reconocida y la regla aplicada.</returns>
    ShapefileLayerDetectionDto Detect(string shpFileName);
}
