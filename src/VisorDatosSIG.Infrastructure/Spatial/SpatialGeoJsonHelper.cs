using NetTopologySuite.Geometries;

namespace VisorDatosSIG.Infrastructure.Spatial;

/// <summary>
/// Convertidor de geometrías NetTopologySuite a estructuras compatibles con GeoJSON RFC 7946.
/// </summary>
public static class SpatialGeoJsonHelper
{
    public static object? ToGeoJsonObject(Geometry? geometry)
    {
        if (geometry is null || geometry.IsEmpty)
        {
            return null;
        }

        return geometry switch
        {
            Point p => new
            {
                type = "Point",
                coordinates = new[] { p.X, p.Y }
            },
            LineString ls => new
            {
                type = "LineString",
                coordinates = ls.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray()
            },
            MultiLineString mls => new
            {
                type = "MultiLineString",
                coordinates = mls.Geometries
                    .OfType<LineString>()
                    .Select(line => line.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray())
                    .ToArray()
            },
            Polygon poly => new
            {
                type = "Polygon",
                coordinates = GetPolygonCoordinates(poly)
            },
            MultiPolygon mp => new
            {
                type = "MultiPolygon",
                coordinates = mp.Geometries
                    .OfType<Polygon>()
                    .Select(GetPolygonCoordinates)
                    .ToArray()
            },
            _ => new
            {
                type = geometry.GeometryType,
                coordinates = geometry.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray()
            }
        };
    }

    private static double[][][] GetPolygonCoordinates(Polygon poly)
    {
        var rings = new List<double[][]>
        {
            poly.ExteriorRing.Coordinates.Select(c => new[] { c.X, c.Y }).ToArray()
        };

        for (var i = 0; i < poly.NumInteriorRings; i++)
        {
            rings.Add(poly.GetInteriorRingN(i).Coordinates.Select(c => new[] { c.X, c.Y }).ToArray());
        }

        return rings.ToArray();
    }
}
