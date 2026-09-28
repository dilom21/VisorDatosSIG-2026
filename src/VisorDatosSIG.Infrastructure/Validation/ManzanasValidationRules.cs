using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Infrastructure.Validation;

/// <summary>
/// Reglas específicas de la capa Manzanas.
/// </summary>
/// <remarks>
/// Los valores NULL de UV_MZA, UV y MZA y el valor Id = 0 fueron confirmados por el
/// diagnóstico y son aceptados por el diseño: se informan como estadísticas de calidad
/// y nunca provocan la omisión del registro.
/// </remarks>
internal sealed class ManzanasValidationRules : LayerValidationRules
{
    private long _idZero;
    private long _uvMzaNull;
    private long _uvNull;
    private long _mzaNull;

    /// <inheritdoc />
    public override ShapefileLayer Layer => ShapefileLayer.Manzanas;

    /// <inheritdoc />
    public override DuplicateRule DuplicateRule => DuplicateRule.GeometryOnly;

    /// <inheritdoc />
    public override void InspectRecord(IReadOnlyDictionary<string, string?> values)
    {
        if (ValidationValueInspector.IsNumericZero(GetValue(values, "Id")))
        {
            _idZero++;
        }

        if (ValidationValueInspector.IsBlank(GetValue(values, "UV_MZA")))
        {
            _uvMzaNull++;
        }

        if (ValidationValueInspector.IsBlank(GetValue(values, "UV")))
        {
            _uvNull++;
        }

        if (ValidationValueInspector.IsBlank(GetValue(values, "MZA")))
        {
            _mzaNull++;
        }
    }

    /// <inheritdoc />
    public override IReadOnlyList<ValidationStatisticDto> BuildStatistics() =>
    [
        ValidationStatisticDto.FromCount(
            "mza-id-cero",
            "Id = 0",
            _idZero,
            ValidationSeverity.Info,
            "No invalida el registro: en la futura transformación se almacenará IdOrigen = NULL."),
        ValidationStatisticDto.FromCount("mza-uvmza-nulo", "UV_MZA NULL", _uvMzaNull, note: "Aceptado por el diseño."),
        ValidationStatisticDto.FromCount("mza-uv-nulo", "UV NULL", _uvNull, note: "Aceptado por el diseño."),
        ValidationStatisticDto.FromCount("mza-mza-nulo", "MZA NULL", _mzaNull, note: "Aceptado por el diseño.")
    ];

    /// <inheritdoc />
    public override IReadOnlyList<RecordValidationIssueDto> BuildQualityIssues()
    {
        var issues = new List<RecordValidationIssueDto>(2);

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

        if (_uvMzaNull > 0 || _uvNull > 0 || _mzaNull > 0)
        {
            issues.Add(CreateQualityIssue(
                ValidationIssueType.AttributeQuality,
                ValidationSeverity.Info,
                $"Valores NULL aceptados por el diseño en los campos de identificación: " +
                $"UV_MZA = {_uvMzaNull:N0}, UV = {_uvNull:N0}, MZA = {_mzaNull:N0}.",
                "Conservar los NULL tal como están y migrarlos como NULL.",
                "UV_MZA / UV / MZA",
                $"{_uvMzaNull:N0} / {_uvNull:N0} / {_mzaNull:N0}"));
        }

        return issues;
    }
}
