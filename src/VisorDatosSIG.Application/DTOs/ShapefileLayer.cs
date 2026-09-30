namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Capas oficiales contempladas por el VisorDatosSIG 2026.
/// <see cref="Unrecognized"/> indica que el nombre del archivo no corresponde a ninguna capa conocida.
/// </summary>
public enum ShapefileLayer
{
    /// <summary>El archivo no corresponde a ninguna capa oficial.</summary>
    Unrecognized = 0,

    /// <summary>Exp_MapaBase_MZA_4326.shp</summary>
    Manzanas = 1,

    /// <summary>Exp_MapaBase_LOTES_4326.shp</summary>
    Lotes = 2,

    /// <summary>Exp_CodigoFijo_4326.shp</summary>
    CodigosFijos = 3,

    /// <summary>Exp_MapaBase_VIAS_4326.shp</summary>
    Vias = 4
}

/// <summary>
/// Nombres visibles de las capas para la interfaz del Migrador.
/// </summary>
public static class ShapefileLayerExtensions
{
    /// <summary>
    /// Devuelve el nombre de la capa tal como debe mostrarse al usuario.
    /// </summary>
    public static string ToDisplayName(this ShapefileLayer layer) => layer switch
    {
        ShapefileLayer.Manzanas => "Manzanas",
        ShapefileLayer.Lotes => "Lotes",
        ShapefileLayer.CodigosFijos => "Códigos Fijos",
        ShapefileLayer.Vias => "Vías",
        _ => "No reconocida"
    };
}
