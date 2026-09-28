using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Infrastructure.Validation;

/// <summary>
/// Reglas específicas de la capa Lotes.
/// </summary>
/// <remarks>
/// <para>
/// La geometría inválida (caso NroLote = L11 en el diagnóstico) se detecta por la
/// validez topológica de la geometría, no por un valor de atributo: la regla vive en
/// <see cref="ShapefileValidator"/> y aquí solo se aporta el contexto del registro.
/// </para>
/// <para>
/// Id = 0 y NroLote = "0" son valores permitidos: se informan como estadísticas y
/// no provocan omisión ni conversión a NULL.
/// </para>
/// </remarks>
internal sealed class LotesValidationRules : LayerValidationRules
{
    private const string CampoNroLote = "NroLote";

    private long _idZero;
    private long _nroLoteCero;
    private long _nroLoteNull;

    /// <inheritdoc />
    public override ShapefileLayer Layer => ShapefileLayer.Lotes;

    /// <inheritdoc />
    public override DuplicateRule DuplicateRule => DuplicateRule.GeometryOnly;

    /// <inheritdoc />
    public override void InspectRecord(IReadOnlyDictionary<string, string?> values)
    {
        if (ValidationValueInspector.IsNumericZero(GetValue(values, "Id")))
        {
            _idZero++;
        }

        var nroLote = GetValue(values, CampoNroLote);
        if (ValidationValueInspector.IsBlank(nroLote))
        {
            _nroLoteNull++;
        }
        else if (ValidationValueInspector.IsNumericZero(nroLote))
        {
            _nroLoteCero++;
        }
    }

    /// <inheritdoc />
    public override IReadOnlyList<ValidationStatisticDto> BuildStatistics() =>
    [
        ValidationStatisticDto.FromCount(
            "lote-id-cero",
            "Id = 0",
            _idZero,
            ValidationSeverity.Info,
            "No invalida el registro: en la futura transformación se almacenará IdOrigen = NULL."),
        ValidationStatisticDto.FromCount(
            "lote-nrolote-cero",
            "NroLote = \"0\"",
            _nroLoteCero,
            ValidationSeverity.Info,
            "Valor especial permitido: no se convierte a NULL ni se omite el registro."),
        ValidationStatisticDto.FromCount("lote-nrolote-nulo", "NroLote NULL", _nroLoteNull)
    ];

    /// <inheritdoc />
    public override IReadOnlyList<RecordValidationIssueDto> BuildQualityIssues()
    {
        var issues = new List<RecordValidationIssueDto>(2);

        if (_nroLoteCero > 0)
        {
            issues.Add(CreateQualityIssue(
                ValidationIssueType.AttributeQuality,
                ValidationSeverity.Info,
                $"{_nroLoteCero:N0} registro(s) tienen NroLote = \"0\", valor especial del origen. " +
                "No es un error y no se omite el registro.",
                "Conservar el valor \"0\" y definir en la fase de migración si representa \"sin número\".",
                CampoNroLote,
                "0"));
        }

        if (_idZero > 0)
        {
            issues.Add(CreateQualityIssue(
                ValidationIssueType.AttributeQuality,
                ValidationSeverity.Info,
                $"El campo Id es 0 en {_idZero:N0} registro(s). No invalida los registros: " +
                "en la futura transformación se almacenará IdOrigen = NULL.",
                "Conservar el registro y aplicar la regla IdOrigen = NULL en la fase de migración.",
                "Id",
                "0"));
        }

        return issues;
    }

    /// <inheritdoc />
    public override string BuildRecordContext(IReadOnlyDictionary<string, string?> values)
    {
        var nroLote = GetValue(values, CampoNroLote);
        return ValidationValueInspector.IsBlank(nroLote)
            ? string.Empty
            : $"{CampoNroLote} = {ValidationValueInspector.Describe(nroLote)}";
    }
}
