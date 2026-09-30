using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Infrastructure.Validation;

/// <summary>
/// Reglas específicas de la capa Vías.
/// </summary>
/// <remarks>
/// Las redundancias y los campos vacíos del diagnóstico (name/Nombre, osm_id/OSMID,
/// highway NULL, maxspeed = 0, ref NULL) se informan como estadísticas de calidad:
/// no invalidan ni omiten registros y no se deduplica por osm_id ni por OBJECTID.
/// </remarks>
internal sealed class ViasValidationRules : LayerValidationRules
{
    private const string CampoNombre = "Nombre";
    private const string CampoOsmId = "osm_id";
    private const string CampoOsmIdMayusculas = "OSMID";
    private const string CampoName = "name";
    private const string CampoHighway = "highway";
    private const string CampoRef = "ref";
    private const string CampoMaxSpeed = "maxspeed";

    private long _nameEqualsNombre;
    private long _osmIdEqualsOsmId;
    private long _nameNull;
    private long _nombreNull;
    private long _highwayNull;
    private long _refNull;
    private long _maxSpeedCero;

    /// <inheritdoc />
    public override ShapefileLayer Layer => ShapefileLayer.Vias;

    /// <inheritdoc />
    public override DuplicateRule DuplicateRule => DuplicateRule.GeometryOnly;

    /// <inheritdoc />
    public override void InspectRecord(IReadOnlyDictionary<string, string?> values)
    {
        var name = GetValue(values, CampoName);
        var nombre = GetValue(values, CampoNombre);

        if (ValidationValueInspector.AreEquivalent(name, nombre))
        {
            _nameEqualsNombre++;
        }

        if (ValidationValueInspector.IsBlank(name))
        {
            _nameNull++;
        }

        if (ValidationValueInspector.IsBlank(nombre))
        {
            _nombreNull++;
        }

        if (ValidationValueInspector.AreEquivalent(GetValue(values, CampoOsmId), GetValue(values, CampoOsmIdMayusculas)))
        {
            _osmIdEqualsOsmId++;
        }

        if (ValidationValueInspector.IsBlank(GetValue(values, CampoHighway)))
        {
            _highwayNull++;
        }

        if (ValidationValueInspector.IsBlank(GetValue(values, CampoRef)))
        {
            _refNull++;
        }

        if (ValidationValueInspector.IsNumericZero(GetValue(values, CampoMaxSpeed)))
        {
            _maxSpeedCero++;
        }
    }

    /// <inheritdoc />
    public override IReadOnlyList<ValidationStatisticDto> BuildStatistics() =>
    [
        ValidationStatisticDto.FromCount(
            "vias-name-nombre",
            "name = Nombre",
            _nameEqualsNombre,
            note: "Redundancia de origen: no invalida ni omite registros."),
        ValidationStatisticDto.FromCount(
            "vias-osmid",
            "osm_id = OSMID",
            _osmIdEqualsOsmId,
            note: "Redundancia de origen: no invalida ni omite registros."),
        ValidationStatisticDto.FromCount(
            "vias-name-nulo",
            "name NULL",
            _nameNull,
            note: "Aceptado: no se omite el registro."),
        ValidationStatisticDto.FromCount("vias-nombre-nulo", "Nombre NULL", _nombreNull),
        ValidationStatisticDto.FromCount(
            "vias-highway-nulo",
            "highway NULL",
            _highwayNull,
            note: "Campo sin información útil: no afecta la migración."),
        ValidationStatisticDto.FromCount(
            "vias-ref-nulo",
            "ref NULL",
            _refNull,
            note: "Campo sin información útil: no afecta la migración."),
        ValidationStatisticDto.FromCount(
            "vias-maxspeed-cero",
            "maxspeed = 0",
            _maxSpeedCero,
            note: "Valor sin información útil: no afecta la migración.")
    ];

    /// <inheritdoc />
    public override IReadOnlyList<RecordValidationIssueDto> BuildQualityIssues()
    {
        var issues = new List<RecordValidationIssueDto>(3);

        if (_nameEqualsNombre > 0)
        {
            issues.Add(CreateQualityIssue(
                ValidationIssueType.AttributeQuality,
                ValidationSeverity.Info,
                $"Los campos name y Nombre contienen la misma información en {_nameEqualsNombre:N0} registro(s): " +
                "redundancia de origen.",
                "Migrar únicamente name hacia Nombre y no trasladar el atributo duplicado.",
                "name / Nombre",
                $"{_nameEqualsNombre:N0} registro(s)"));
        }

        if (_osmIdEqualsOsmId > 0)
        {
            issues.Add(CreateQualityIssue(
                ValidationIssueType.AttributeQuality,
                ValidationSeverity.Info,
                $"Los campos osm_id y OSMID contienen la misma información en {_osmIdEqualsOsmId:N0} registro(s): " +
                "redundancia de origen.",
                "Migrar únicamente osm_id y no trasladar el atributo duplicado. " +
                "No asumir que osm_id o OBJECTID son claves globales.",
                "osm_id / OSMID",
                $"{_osmIdEqualsOsmId:N0} registro(s)"));
        }

        if (_nameNull > 0 || _nombreNull > 0)
        {
            issues.Add(CreateQualityIssue(
                ValidationIssueType.AttributeQuality,
                ValidationSeverity.Info,
                $"name / Nombre son NULL en {_nameNull:N0} / {_nombreNull:N0} registro(s): " +
                "los registros se conservan.",
                "Migrar los valores NULL tal como están.",
                CampoName,
                $"{_nameNull:N0} registro(s)"));
        }

        if (_highwayNull > 0 || _refNull > 0 || _maxSpeedCero > 0)
        {
            issues.Add(CreateQualityIssue(
                ValidationIssueType.AttributeQuality,
                ValidationSeverity.Info,
                "Campos del origen sin información útil: " +
                $"highway NULL = {_highwayNull:N0}, ref NULL = {_refNull:N0}, maxspeed = 0 = {_maxSpeedCero:N0}.",
                "No trasladar estos campos en la migración.",
                "highway / ref / maxspeed",
                $"{_highwayNull:N0} / {_refNull:N0} / {_maxSpeedCero:N0}"));
        }

        return issues;
    }

    /// <inheritdoc />
    public override string BuildRecordContext(IReadOnlyDictionary<string, string?> values)
    {
        var name = GetValue(values, CampoName);
        var osmId = GetValue(values, CampoOsmId);

        if (ValidationValueInspector.IsBlank(name) && ValidationValueInspector.IsBlank(osmId))
        {
            return string.Empty;
        }

        return $"{CampoName} = {ValidationValueInspector.Describe(name)}, " +
               $"osm_id = {ValidationValueInspector.Describe(osmId)}";
    }
}
