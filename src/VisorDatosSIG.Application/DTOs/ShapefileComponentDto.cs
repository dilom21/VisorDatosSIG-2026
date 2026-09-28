namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Archivos que componen un shapefile.
/// </summary>
public enum ShapefileComponentType
{
    /// <summary>Geometrías (.shp).</summary>
    Shp = 1,

    /// <summary>Índice espacial (.shx).</summary>
    Shx = 2,

    /// <summary>Atributos dBASE (.dbf).</summary>
    Dbf = 3,

    /// <summary>Referencia espacial en texto WKT (.prj).</summary>
    Prj = 4
}

/// <summary>
/// Estado de cada archivo asociado al shapefile seleccionado.
/// </summary>
public sealed class ShapefileComponentDto
{
    /// <summary>Tipo de componente.</summary>
    public required ShapefileComponentType Type { get; init; }

    /// <summary>Extensión del componente, por ejemplo ".dbf".</summary>
    public required string Extension { get; init; }

    /// <summary>Descripción funcional del componente.</summary>
    public required string Description { get; init; }

    /// <summary>Indica si el componente es obligatorio para poder leer el shapefile.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Indica si el archivo existe en el disco.</summary>
    public required bool Exists { get; init; }

    /// <summary>Ruta esperada del componente.</summary>
    public string FilePath { get; init; } = string.Empty;
}
