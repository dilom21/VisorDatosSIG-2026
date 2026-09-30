using System.Globalization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Dbf.Fields;
using VisorDatosSIG.Application.DTOs;
using EsriShapefileReader = NetTopologySuite.IO.Esri.Shapefiles.Readers.ShapefileReader;
using EsriShapefileReaderOptions = NetTopologySuite.IO.Esri.Shapefiles.Readers.ShapefileReaderOptions;
using EsriGeometryBuilderMode = NetTopologySuite.IO.Esri.Shapefiles.Readers.GeometryBuilderMode;

namespace VisorDatosSIG.Infrastructure.Shapefiles;

/// <summary>
/// Acceso común a los archivos del shapefile y utilidades compartidas entre la
/// inspección (Fase 1) y la validación detallada (Fase 2).
/// </summary>
/// <remarks>
/// Centraliza la apertura en modo solo lectura y las conversiones que deben ser
/// idénticas en ambas fases, evitando duplicar lógica.
/// </remarks>
internal static class ShapefileAccess
{
    /// <summary>
    /// Crea las opciones de lectura utilizadas en todo el proyecto.
    /// </summary>
    /// <remarks>
    /// Se usa <see cref="EsriGeometryBuilderMode.IgnoreInvalidShapes"/>: los registros se leen
    /// tal como están en el archivo. No se reparan ni se descartan geometrías inválidas
    /// (su tratamiento corresponde a la validación, que las marca como omitibles).
    /// </remarks>
    public static EsriShapefileReaderOptions CreateReaderOptions() => new()
    {
        GeometryBuilderMode = EsriGeometryBuilderMode.IgnoreInvalidShapes
    };

    /// <summary>
    /// Abre el shapefile en modo solo lectura.
    /// </summary>
    public static EsriShapefileReader OpenReader(string shpPath) =>
        Shapefile.OpenRead(shpPath, CreateReaderOptions());

    /// <summary>
    /// Convierte las definiciones de campos del DBF en el DTO que utiliza la aplicación.
    /// </summary>
    public static IReadOnlyList<ShapefileFieldDto> MapFields(DbfFieldCollection dbfFields)
    {
        var fields = new List<ShapefileFieldDto>(dbfFields.Count);
        for (var index = 0; index < dbfFields.Count; index++)
        {
            var field = dbfFields[index];
            fields.Add(new ShapefileFieldDto
            {
                Index = index,
                Name = field.Name,
                DataType = field.FieldType.ToString(),
                Length = field.Length,
                DecimalCount = field.NumericScale
            });
        }

        return fields;
    }

    /// <summary>
    /// Copia los valores del registro actual del lector al diccionario indicado.
    /// </summary>
    /// <remarks>
    /// Se reutiliza un único diccionario durante el recorrido para reducir asignaciones.
    /// </remarks>
    public static void FillRecordValues(
        EsriShapefileReader reader,
        IReadOnlyList<ShapefileFieldDto> fields,
        IDictionary<string, string?> destination)
    {
        destination.Clear();
        for (var index = 0; index < fields.Count; index++)
        {
            var value = index < reader.Fields.Count ? reader.Fields[index].Value : null;
            destination[fields[index].Name] = FormatAttributeValue(value);
        }
    }

    /// <summary>
    /// Convierte el valor de un campo del DBF a texto sin modificar su contenido
    /// (no se recortan espacios porque el proyecto no debe corregir valores silenciosamente).
    /// </summary>
    public static string? FormatAttributeValue(object? value) => value switch
    {
        null => null,
        string text => text,
        DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool boolean => boolean ? "true" : "false",
        double number => number.ToString("0.############", CultureInfo.InvariantCulture),
        float number => number.ToString("0.############", CultureInfo.InvariantCulture),
        decimal number => number.ToString("0.############", CultureInfo.InvariantCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    /// <summary>
    /// Da formato a la extensión (bounding box) declarada en el encabezado del SHP.
    /// </summary>
    public static string? FormatExtent(Envelope? envelope)
    {
        if (envelope is null || envelope.IsNull)
        {
            return null;
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "X: {0:0.######} a {1:0.######} | Y: {2:0.######} a {3:0.######}",
            envelope.MinX,
            envelope.MaxX,
            envelope.MinY,
            envelope.MaxY);
    }

    /// <summary>
    /// Verifica si la extensión está dentro del rango válido de coordenadas geográficas.
    /// </summary>
    public static bool? IsWithinGeographicRange(Envelope? envelope)
    {
        if (envelope is null || envelope.IsNull)
        {
            return null;
        }

        return envelope.MinX >= -180 && envelope.MaxX <= 180
               && envelope.MinY >= -90 && envelope.MaxY <= 90;
    }

    /// <summary>
    /// Describe el tipo de geometría declarado en el encabezado del archivo SHP.
    /// </summary>
    public static string DescribeShapeType(ShapeType shapeType)
    {
        var kind = shapeType switch
        {
            ShapeType.NullShape => "nulo",
            _ when shapeType.IsPoint() => "punto",
            _ when shapeType.IsMultiPoint() => "multipunto",
            _ when shapeType.IsPolyLine() => "línea",
            _ when shapeType.IsPolygon() => "polígono",
            _ => "desconocido"
        };

        var dimensions = new List<string>(2);
        if (shapeType.HasZ())
        {
            dimensions.Add("con Z");
        }

        if (shapeType.HasM())
        {
            dimensions.Add("con M");
        }

        var suffix = dimensions.Count > 0 ? $", {string.Join(", ", dimensions)}" : string.Empty;
        return $"{shapeType} ({kind}{suffix})";
    }

    /// <summary>
    /// Compara el tipo de geometría declarado por el archivo con el esperado para la capa.
    /// </summary>
    public static bool MatchesExpectedShapeType(ShapefileLayer layer, ShapeType shapeType) => layer switch
    {
        ShapefileLayer.Manzanas or ShapefileLayer.Lotes => shapeType.IsPolygon(),
        ShapefileLayer.CodigosFijos => shapeType.IsPoint(),
        ShapefileLayer.Vias => shapeType.IsPolyLine(),
        _ => true
    };

    /// <summary>
    /// Nombres de geometría admitidos por capa (según NetTopologySuite).
    /// Las variantes con Z/M comparten el mismo nombre de geometría, por lo que
    /// una geometría con Z o M no se considera inválida por ese motivo.
    /// </summary>
    public static IReadOnlyList<string> ExpectedGeometryNames(ShapefileLayer layer) => layer switch
    {
        ShapefileLayer.Manzanas or ShapefileLayer.Lotes => ["Polygon", "MultiPolygon"],
        ShapefileLayer.CodigosFijos => ["Point"],
        ShapefileLayer.Vias => ["LineString", "MultiLineString"],
        _ => []
    };

    /// <summary>
    /// Descripción de la geometría esperada para la capa detectada.
    /// </summary>
    public static string ExpectedGeometryDescription(ShapefileLayer layer) => layer switch
    {
        ShapefileLayer.Manzanas or ShapefileLayer.Lotes => "polígono / multipolígono",
        ShapefileLayer.CodigosFijos => "punto",
        ShapefileLayer.Vias => "línea / multilínea",
        _ => "sin restricción"
    };

    /// <summary>
    /// Verifica el nombre de geometría de un registro contra el esperado para la capa.
    /// </summary>
    public static bool MatchesExpectedGeometryName(ShapefileLayer layer, string geometryName)
    {
        var expected = ExpectedGeometryNames(layer);
        return expected.Count == 0 || expected.Contains(geometryName, StringComparer.Ordinal);
    }

    /// <summary>
    /// Describe una excepción técnica de forma breve y comprensible.
    /// </summary>
    public static string DescribeException(Exception exception) =>
        $"{exception.GetType().Name}: {exception.Message}";
}
