using System.Globalization;
using System.Text.RegularExpressions;
using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Infrastructure.Shapefiles;

/// <summary>
/// Interpreta el contenido del archivo .prj (texto WKT) para determinarlo
/// la referencia espacial del shapefile.
/// </summary>
/// <remarks>
/// La detección es deliberadamente conservadora:
/// <list type="bullet">
/// <item>Si el WKT declara explícitamente un código EPSG se utiliza ese valor.</item>
/// <item>Si el WKT corresponde al sistema geográfico WGS 84 se asume EPSG:4326,
/// ya que WGS 84 geográfico es EPSG:4326.</item>
/// <item>En cualquier otro caso el código EPSG queda sin determinar.</item>
/// </list>
/// Toda la lógica está encapsulada en esta clase para poder mejorarla luego
/// (por ejemplo, incorporando un catálogo EPSG completo) sin afectar al lector.
/// </remarks>
public static class PrjSpatialReferenceReader
{
    private const int Wgs84Srid = 4326;

    /// <summary>Evidencias textuales de WGS 84 en el WKT de Esri y de OGC.</summary>
    private static readonly string[] Wgs84Tokens =
        ["WGS_1984", "WGS_84", "WGS84", "WGS 1984", "WGS 84", "D_WGS_1984"];

    private static readonly string[] GeographicTokens = ["GEOGCS", "GEOGCRS", "GEODCRS"];

    private static readonly string[] ProjectedTokens = ["PROJCS", "PROJCRS", "PROJECTEDCRS"];

    private static readonly Regex EpsgAuthorityRegex = new(
        "(?:AUTHORITY|ID)\\s*\\[\\s*\"?EPSG\"?\\s*,\\s*\"?(?<code>\\d{3,6})\"?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Interpreta el contenido del archivo .prj.
    /// </summary>
    /// <param name="prjContent">Contenido del archivo .prj o <c>null</c> cuando no está disponible.</param>
    public static SpatialReferenceDto Parse(string? prjContent)
    {
        if (string.IsNullOrWhiteSpace(prjContent))
        {
            return new SpatialReferenceDto
            {
                IsAvailable = false,
                Note = "No se dispone del archivo .prj: no es posible verificar la referencia espacial del shapefile."
            };
        }

        var wkt = prjContent.Trim();
        var upperWkt = wkt.ToUpperInvariant();

        var isProjected = ContainsAny(upperWkt, ProjectedTokens);
        var isGeographic = ContainsAny(upperWkt, GeographicTokens) && !isProjected;
        var isWgs84 = ContainsAny(upperWkt, Wgs84Tokens);

        var declaredSrid = TryReadEpsgCode(wkt);
        int? srid = declaredSrid;
        string evidence;
        string? note = null;

        if (declaredSrid.HasValue)
        {
            evidence = $"El WKT declara explícitamente el código EPSG:{declaredSrid.Value}.";
        }
        else if (isWgs84 && isGeographic)
        {
            srid = Wgs84Srid;
            evidence = "El WKT corresponde al sistema geográfico WGS 84, equivalente a EPSG:4326.";
        }
        else
        {
            evidence = "El WKT no declara un código EPSG ni corresponde a WGS 84 geográfico.";
            note = "No se pudo determinar el código EPSG a partir del archivo .prj.";
        }

        if (isProjected)
        {
            note = "El archivo .prj describe un sistema de coordenadas proyectado; " +
                   "las capas oficiales se esperan en WGS 84 geográfico (EPSG:4326).";
        }
        else if (isGeographic && !isWgs84)
        {
            note = "El archivo .prj describe un sistema geográfico distinto de WGS 84.";
        }

        return new SpatialReferenceDto
        {
            IsAvailable = true,
            Srid = srid,
            Name = ReadCrsName(wkt),
            Wkt = wkt,
            IsGeographic = isGeographic,
            IsWgs84 = isWgs84,
            DetectionEvidence = evidence,
            Note = note
        };
    }

    /// <summary>
    /// Busca un código EPSG declarado en el WKT (AUTHORITY["EPSG","4326"] o ID["EPSG",4326]).
    /// </summary>
    private static int? TryReadEpsgCode(string wkt)
    {
        var match = EpsgAuthorityRegex.Match(wkt);
        if (match.Success && int.TryParse(match.Groups["code"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
        {
            return code;
        }

        return null;
    }

    /// <summary>
    /// Obtiene el nombre del sistema de coordenadas, que en WKT está en el primer texto entre comillas.
    /// </summary>
    private static string? ReadCrsName(string wkt)
    {
        var start = wkt.IndexOf('"');
        if (start < 0)
        {
            return null;
        }

        var end = wkt.IndexOf('"', start + 1);
        return end > start ? wkt[(start + 1)..end] : null;
    }

    private static bool ContainsAny(string text, string[] tokens) =>
        tokens.Any(token => text.Contains(token, StringComparison.Ordinal));
}
