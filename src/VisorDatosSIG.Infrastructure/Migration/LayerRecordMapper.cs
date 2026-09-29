using System.Globalization;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri.Shapefiles.Readers;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Infrastructure.Shapefiles;
using EsriShapefileReader = NetTopologySuite.IO.Esri.Shapefiles.Readers.ShapefileReader;

namespace VisorDatosSIG.Infrastructure.Migration;

internal static class LayerRecordMapper
{
    public static string GetDestinationTable(ShapefileLayer layer) => layer switch
    {
        ShapefileLayer.Manzanas => "dbo.Manzanas",
        ShapefileLayer.Lotes => "dbo.Lotes",
        ShapefileLayer.CodigosFijos => "dbo.CodigosFijos",
        ShapefileLayer.Vias => "dbo.Vias",
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "La capa no está reconocida.")
    };

    public static MigrationRow Map(
        ShapefileLayer layer,
        EsriShapefileReader reader,
        IReadOnlyList<ShapefileFieldDto> fields)
    {
        var geometry = reader.Geometry;
        if (geometry is null || geometry.IsEmpty)
        {
            throw new InvalidDataException("El registro no contiene una geometría migrable.");
        }

        if (!ShapefileAccess.MatchesExpectedGeometryName(layer, geometry.GeometryType))
        {
            throw new InvalidDataException($"La geometría '{geometry.GeometryType}' no corresponde a la capa {layer.ToDisplayName()}.");
        }

        var values = new Dictionary<string, string?>(fields.Count, StringComparer.OrdinalIgnoreCase);
        ShapefileAccess.FillRecordValues(reader, fields, values);
        var migratedGeometry = (Geometry)geometry.Copy();
        migratedGeometry.SRID = 4326;

        return layer switch
        {
            ShapefileLayer.Manzanas => MapManzana(values, migratedGeometry),
            ShapefileLayer.Lotes => MapLote(values, migratedGeometry),
            ShapefileLayer.CodigosFijos => MapCodigoFijo(values, migratedGeometry),
            ShapefileLayer.Vias => MapVia(values, migratedGeometry),
            _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "La capa no está reconocida.")
        };
    }

    private static MigrationRow MapManzana(IReadOnlyDictionary<string, string?> values, Geometry geometry) => new()
    {
        Geometry = geometry,
        Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["IdOrigen"] = NullableInt(values, "Id", "Manzanas.IdOrigen"),
            ["UV_MZA"] = NullableString(values, "UV_MZA", 20, "Manzanas.UV_MZA"),
            ["UV"] = NullableString(values, "UV", 15, "Manzanas.UV"),
            ["MZA"] = NullableString(values, "MZA", 10, "Manzanas.MZA")
        }
    };

    private static MigrationRow MapLote(IReadOnlyDictionary<string, string?> values, Geometry geometry) => new()
    {
        Geometry = geometry,
        Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["IdOrigen"] = NullableInt(values, "Id", "Lotes.IdOrigen"),
            ["NroLote"] = NullableString(values, "NroLote", 15, "Lotes.NroLote"),
            ["IdManzana"] = null
        }
    };

    private static MigrationRow MapCodigoFijo(IReadOnlyDictionary<string, string?> values, Geometry geometry) => new()
    {
        Geometry = geometry,
        Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["CodF_SQL"] = NullableInt64AsInt(values, "CodF_SQL", "CodigosFijos.CodF_SQL"),
            ["CodF_SIG"] = NullableString(values, "CodF_SIG", 25, "CodigosFijos.CodF_SIG"),
            ["CodFijo"] = NullableInt(values, "CodFijo", "CodigosFijos.CodFijo"),
            ["Nombre"] = NullableString(values, "Nombre", 120, "CodigosFijos.Nombre"),
            ["IdLote"] = null,
            ["Longitud"] = geometry.Coordinate?.X,
            ["Latitud"] = geometry.Coordinate?.Y
        }
    };

    private static MigrationRow MapVia(IReadOnlyDictionary<string, string?> values, Geometry geometry) => new()
    {
        Geometry = geometry,
        Values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["OBJECTID"] = NullableInt(values, "OBJECTID", "Vias.OBJECTID"),
            ["Nombre"] = NullableString(values, "name", 40, "Vias.Nombre"),
            ["TipoVia"] = NullableString(values, "type", 30, "Vias.TipoVia"),
            ["OSMID"] = NullableString(values, "osm_id", 20, "Vias.OSMID")
        }
    };

    private static int? NullableInt(IReadOnlyDictionary<string, string?> values, string field, string destination)
    {
        var value = GetValue(values, field);
        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "0" && field.Equals("Id", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        throw new InvalidDataException($"El campo '{field}' no contiene un entero válido para {destination}: '{value}'.");
    }

    private static int? NullableInt64AsInt(IReadOnlyDictionary<string, string?> values, string field, string destination)
    {
        var value = GetValue(values, field);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            || parsed < int.MinValue
            || parsed > int.MaxValue)
        {
            throw new InvalidDataException($"El campo '{field}' está fuera del rango INT para {destination}: '{value}'.");
        }

        return (int)parsed;
    }

    private static string? NullableString(
        IReadOnlyDictionary<string, string?> values,
        string field,
        int maxLength,
        string destination)
    {
        var value = GetValue(values, field);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Length > maxLength)
        {
            throw new InvalidDataException($"El campo '{field}' supera la longitud de {destination}: {value.Length}/{maxLength}.");
        }

        return value;
    }

    private static string? GetValue(IReadOnlyDictionary<string, string?> values, string field) =>
        values.TryGetValue(field, out var value) ? value : null;
}
