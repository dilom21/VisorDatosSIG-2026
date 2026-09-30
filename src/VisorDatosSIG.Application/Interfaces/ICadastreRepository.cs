using VisorDatosSIG.Application.Common;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato para consultas espaciales y alfanuméricas de catastro y capas geográficas (CU08-CU19).
/// </summary>
public interface ICadastreRepository
{
    /// <summary>
    /// Obtiene las entidades de una capa en formato GeoJSON FeatureCollection, opcionalmente filtradas por bounding box.
    /// </summary>
    Task<GeoJsonFeatureCollectionDto> GetLayerGeoJsonAsync(
        string layerName,
        double? minX = null,
        double? minY = null,
        double? maxX = null,
        double? maxY = null,
        int limit = 2000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifica las entidades situadas bajo o cerca de una coordenada geográfica (clic en el mapa, RF-CON-01).
    /// </summary>
    Task<IReadOnlyList<GeoJsonFeatureDto>> IdentifyAsync(
        double longitude,
        double latitude,
        double toleranceMeters = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Búsqueda alfanumérica insensible a mayúsculas/minúsculas en todas las capas o una específica (RF-CON-03, RF-CON-04).
    /// </summary>
    Task<PagedResult<IDictionary<string, object?>>> SearchAsync(
        string query,
        string? layer = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los metadatos y conteos de las capas registradas en SQL Server.
    /// </summary>
    Task<IReadOnlyList<IDictionary<string, object?>>> GetLayersCatalogAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene la envolvente / extensión espacial (minX, minY, maxX, maxY) de una capa o del conjunto de capas (CU19).
    /// </summary>
    Task<double[]?> GetLayerExtentAsync(string? layerName = null, CancellationToken cancellationToken = default);
}
