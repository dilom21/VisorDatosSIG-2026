using System.Globalization;
using System.Text;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Shapefiles;

/// <summary>
/// Detector centralizado de las capas oficiales del VisorDatosSIG 2026.
/// Las reglas de reconocimiento están declaradas en una única tabla (<see cref="Rules"/>),
/// de modo que agregar o ajustar una capa no requiere modificar la lógica de detección.
/// </summary>
public sealed class ShapefileLayerDetector : IShapefileLayerDetector
{
    /// <summary>
    /// Regla de reconocimiento de una capa oficial.
    /// </summary>
    /// <param name="Layer">Capa oficial.</param>
    /// <param name="OfficialFileName">Nombre oficial esperado del archivo .shp.</param>
    /// <param name="Tokens">Palabras clave admitidas en el nombre del archivo.</param>
    private sealed record LayerRule(ShapefileLayer Layer, string OfficialFileName, string[] Tokens);

    /// <summary>
    /// Tabla única de reglas. El orden define la prioridad cuando un nombre coincide con varias capas.
    /// </summary>
    private static readonly LayerRule[] Rules =
    [
        new(ShapefileLayer.Manzanas, "Exp_MapaBase_MZA_4326.shp", ["MZA", "MANZANA", "MANZANAS"]),
        new(ShapefileLayer.Lotes, "Exp_MapaBase_LOTES_4326.shp", ["LOTE", "LOTES"]),
        new(ShapefileLayer.CodigosFijos, "Exp_CodigoFijo_4326.shp", ["CODIGOFIJO", "CODIGOFIJOS", "CODIGOSFIJOS", "CODFIJO"]),
        new(ShapefileLayer.Vias, "Exp_MapaBase_VIAS_4326.shp", ["VIA", "VIAS"])
    ];

    /// <inheritdoc />
    public ShapefileLayerDetectionDto Detect(string shpFileName)
    {
        if (string.IsNullOrWhiteSpace(shpFileName))
        {
            return Unrecognized("No se recibió un nombre de archivo para analizar.");
        }

        var normalizedName = NormalizeFileName(shpFileName);

        // 1) Coincidencia exacta con el nombre oficial de alguna capa.
        foreach (var rule in Rules)
        {
            if (string.Equals(normalizedName, NormalizeFileName(rule.OfficialFileName), StringComparison.Ordinal))
            {
                return CreateResult(rule, $"Coincidencia exacta con el nombre oficial '{rule.OfficialFileName}'.", [rule.Layer]);
            }
        }

        // 2) Coincidencia por palabras clave (permite variantes de nombre o de sufijo de SRID).
        var tokens = normalizedName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var matches = Rules
            .Where(rule => tokens.Any(token => rule.Tokens.Contains(token, StringComparer.Ordinal)))
            .ToArray();

        if (matches.Length > 0)
        {
            var matchedLayers = matches.Select(match => match.Layer).ToArray();
            var appliedRule = $"Coincidencia por palabra clave en el nombre del archivo ('{string.Join("', '", tokens)}').";
            return CreateResult(matches[0], appliedRule, matchedLayers);
        }

        return Unrecognized("El nombre del archivo no contiene palabras clave de ninguna capa oficial.");
    }

    /// <summary>
    /// Normaliza un nombre de archivo: quita la extensión, elimina acentos y
    /// reemplaza cualquier separador por guion bajo, todo en mayúsculas.
    /// </summary>
    /// <param name="fileName">Nombre o ruta del archivo.</param>
    private static string NormalizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Trim());
        if (name.EndsWith(".shp", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        var builder = new StringBuilder(name.Length);
        foreach (var character in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : '_');
        }

        return builder.ToString().Trim('_');
    }

    private static ShapefileLayerDetectionDto CreateResult(
        LayerRule rule,
        string appliedRule,
        IReadOnlyList<ShapefileLayer> matchedLayers) => new()
    {
        Layer = rule.Layer,
        LayerDisplayName = rule.Layer.ToDisplayName(),
        OfficialFileName = rule.OfficialFileName,
        AppliedRule = appliedRule,
        MatchedLayers = matchedLayers
    };

    private static ShapefileLayerDetectionDto Unrecognized(string appliedRule) => new()
    {
        Layer = ShapefileLayer.Unrecognized,
        LayerDisplayName = ShapefileLayer.Unrecognized.ToDisplayName(),
        AppliedRule = appliedRule
    };
}
