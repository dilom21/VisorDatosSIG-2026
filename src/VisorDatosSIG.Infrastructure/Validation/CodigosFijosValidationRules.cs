using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Infrastructure.Validation;

/// <summary>
/// Reglas específicas de la capa Códigos Fijos.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Los atributos Longi y Latid no son una fuente espacial confiable: se contabilizan.</item>
/// <item>Text y CodF_SIG son redundantes en el origen: se contabilizan.</item>
/// <item>CodFijo admite valores repetidos legítimos: no se deduplica por ese campo.</item>
/// <item>
/// El único criterio de duplicado es el duplicado exacto (geometría más todos los campos)
/// y el registro redundante se marca para omitir.
/// </item>
/// </list>
/// </remarks>
internal sealed class CodigosFijosValidationRules : LayerValidationRules
{
    private const string CampoText = "Text";
    private const string CampoCodFSig = "CodF_SIG";
    private const string CampoLongi = "Longi";
    private const string CampoLatid = "Latid";
    private const string CampoCodFijo = "CodFijo";

    /// <summary>Campos comparados además de la geometría para considerar un duplicado exacto.</summary>
    private static readonly string[] CamposDeDuplicadoExacto =
    [
        CampoText,
        "CodF_SQL",
        CampoCodFSig,
        CampoLongi,
        CampoLatid,
        CampoCodFijo,
        "Nombre"
    ];

    private readonly Dictionary<string, long> _codFijoCounts = new(StringComparer.Ordinal);

    private long _longiEqualsLatid;
    private long _textEqualsCodFSig;

    /// <inheritdoc />
    public override ShapefileLayer Layer => ShapefileLayer.CodigosFijos;

    /// <inheritdoc />
    public override DuplicateRule DuplicateRule => DuplicateRule.FullRecord(CamposDeDuplicadoExacto);

    /// <inheritdoc />
    public override void InspectRecord(IReadOnlyDictionary<string, string?> values)
    {
        if (ValidationValueInspector.AreEquivalent(GetValue(values, CampoLongi), GetValue(values, CampoLatid)))
        {
            _longiEqualsLatid++;
        }

        if (ValidationValueInspector.AreEquivalent(GetValue(values, CampoText), GetValue(values, CampoCodFSig)))
        {
            _textEqualsCodFSig++;
        }

        var codFijo = GetValue(values, CampoCodFijo);
        if (!ValidationValueInspector.IsBlank(codFijo))
        {
            var key = codFijo!.Trim();
            _codFijoCounts[key] = _codFijoCounts.GetValueOrDefault(key) + 1;
        }
    }

    /// <inheritdoc />
    public override IReadOnlyList<ValidationStatisticDto> BuildStatistics()
    {
        var valoresRepetidos = _codFijoCounts.Count(par => par.Value > 1);
        var registrosConValorRepetido = _codFijoCounts.Where(par => par.Value > 1).Sum(par => par.Value);

        return
        [
            ValidationStatisticDto.FromCount(
                "cf-longi-latid",
                "Longi = Latid",
                _longiEqualsLatid,
                ValidationSeverity.Warning,
                "Los atributos no son una fuente espacial confiable: la geometría es la referencia. " +
                "La transformación Geometry.X/Y se realizará en la fase de migración."),
            ValidationStatisticDto.FromCount(
                "cf-text-codfsig",
                "Text = CodF_SIG",
                _textEqualsCodFSig,
                note: "Redundancia de origen: no invalida ni omite registros."),
            ValidationStatisticDto.FromCount(
                "cf-codfijo-valores-repetidos",
                "CodFijo con valores repetidos (valores distintos)",
                valoresRepetidos,
                note: "CodFijo no es clave única: no se deduplica por este campo."),
            ValidationStatisticDto.FromCount(
                "cf-codfijo-registros-repetidos",
                "Registros con CodFijo repetido",
                registrosConValorRepetido,
                note: "Repetición legítima: no implica duplicado.")
        ];
    }

    /// <inheritdoc />
    public override IReadOnlyList<RecordValidationIssueDto> BuildQualityIssues()
    {
        var issues = new List<RecordValidationIssueDto>(3);

        if (_longiEqualsLatid > 0)
        {
            issues.Add(CreateQualityIssue(
                ValidationIssueType.AttributeQuality,
                ValidationSeverity.Warning,
                $"Los campos Longi y Latid tienen el mismo valor en {_longiEqualsLatid:N0} registro(s): " +
                "no son una fuente espacial confiable.",
                "Migrar las coordenadas desde Geometry.X / Geometry.Y en la fase de transformación " +
                "(no se transforma en esta fase).",
                "Longi / Latid",
                $"{_longiEqualsLatid:N0} registro(s)"));
        }

        if (_textEqualsCodFSig > 0)
        {
            issues.Add(CreateQualityIssue(
                ValidationIssueType.AttributeQuality,
                ValidationSeverity.Info,
                $"Los campos Text y CodF_SIG contienen la misma información en {_textEqualsCodFSig:N0} registro(s): " +
                "redundancia de origen.",
                "Migrar únicamente CodF_SIG y no trasladar Text.",
                "Text / CodF_SIG",
                $"{_textEqualsCodFSig:N0} registro(s)"));
        }

        if (_codFijoCounts.Values.Any(count => count > 1))
        {
            issues.Add(CreateQualityIssue(
                ValidationIssueType.AttributeQuality,
                ValidationSeverity.Info,
                "El campo CodFijo presenta valores repetidos, situación legítima del origen. " +
                "No se deduplica por CodFijo: solo se omiten los duplicados exactos.",
                "No deduplicar por CodFijo; usar la regla de duplicado exacto.",
                CampoCodFijo,
                "valores repetidos"));
        }

        return issues;
    }

    /// <inheritdoc />
    public override string BuildRecordContext(IReadOnlyDictionary<string, string?> values)
    {
        var codFSig = GetValue(values, CampoCodFSig);
        var codFijo = GetValue(values, CampoCodFijo);

        if (ValidationValueInspector.IsBlank(codFSig) && ValidationValueInspector.IsBlank(codFijo))
        {
            return string.Empty;
        }

        return $"{CampoCodFSig} = {ValidationValueInspector.Describe(codFSig)}, " +
               $"{CampoCodFijo} = {ValidationValueInspector.Describe(codFijo)}";
    }
}
