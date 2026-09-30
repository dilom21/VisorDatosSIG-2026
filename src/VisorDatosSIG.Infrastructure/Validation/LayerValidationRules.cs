using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Infrastructure.Validation;

/// <summary>
/// Regla de detección de duplicados aplicada a una capa.
/// </summary>
/// <param name="IsEnabled">Indica si se analizan duplicados en la capa.</param>
/// <param name="OmitRedundantRecords">Indica si los registros redundantes se marcan para omitir.</param>
/// <param name="ConfirmExactGeometry">
/// Indica si se conserva la geometría de la primera aparición para confirmar la igualdad exacta.
/// Es obligatorio cuando los duplicados redundantes se omiten.
/// </param>
/// <param name="AttributeFields">Campos de atributos que deben coincidir además de la geometría.</param>
/// <param name="Description">Descripción de la regla para el resumen de calidad.</param>
internal sealed record DuplicateRule(
    bool IsEnabled,
    bool OmitRedundantRecords,
    bool ConfirmExactGeometry,
    string[] AttributeFields,
    string Description)
{
    /// <summary>Sin detección de duplicados.</summary>
    public static DuplicateRule None => new(false, false, false, [], "Sin detección de duplicados.");

    /// <summary>
    /// Solo se informan geometrías duplicadas exactas. No se deduplica ni se omiten registros.
    /// </summary>
    public static DuplicateRule GeometryOnly =>
        new(true, false, false, [], "Se informan geometrías duplicadas exactas; no se deduplica.");

    /// <summary>
    /// Se detectan duplicados exactos (geometría y los campos indicados) y los
    /// registros redundantes se marcan para omitir.
    /// </summary>
    public static DuplicateRule FullRecord(string[] attributeFields) =>
        new(true, true, true, attributeFields, "Duplicado exacto = misma geometría y mismos atributos; el registro redundante se omite.");
}

/// <summary>
/// Reglas específicas de calidad por capa.
/// </summary>
/// <remarks>
/// Las reglas de este tipo solo acumulan estadísticas y generan incidencias agregadas:
/// no producen miles de incidencias por situaciones normales (por ejemplo, valores NULL
/// confirmados por el diagnóstico).
/// </remarks>
internal abstract class LayerValidationRules
{
    /// <summary>Capa a la que pertenecen las reglas.</summary>
    public abstract ShapefileLayer Layer { get; }

    /// <summary>Nombre visible de la capa.</summary>
    public virtual string LayerDisplayName => Layer.ToDisplayName();

    /// <summary>Regla de duplicados de la capa.</summary>
    public abstract DuplicateRule DuplicateRule { get; }

    /// <summary>
    /// Analiza los atributos de un registro y acumula estadísticas de calidad.
    /// </summary>
    public abstract void InspectRecord(IReadOnlyDictionary<string, string?> values);

    /// <summary>
    /// Estadísticas acumuladas una vez finalizado el recorrido de registros.
    /// </summary>
    public abstract IReadOnlyList<ValidationStatisticDto> BuildStatistics();

    /// <summary>
    /// Incidencias agregadas de calidad (no corresponden a un registro concreto).
    /// </summary>
    public virtual IReadOnlyList<RecordValidationIssueDto> BuildQualityIssues() => [];

    /// <summary>
    /// Contexto legible del registro, utilizado para enriquecer los mensajes de incidencia.
    /// </summary>
    public virtual string BuildRecordContext(IReadOnlyDictionary<string, string?> values) => string.Empty;

    /// <summary>
    /// Crea el conjunto de reglas correspondiente a la capa detectada.
    /// </summary>
    public static LayerValidationRules CreateFor(ShapefileLayer layer) => layer switch
    {
        ShapefileLayer.Manzanas => new ManzanasValidationRules(),
        ShapefileLayer.Lotes => new LotesValidationRules(),
        ShapefileLayer.CodigosFijos => new CodigosFijosValidationRules(),
        ShapefileLayer.Vias => new ViasValidationRules(),
        _ => new GenericValidationRules()
    };

    /// <summary>Obtiene el valor de un campo del registro.</summary>
    protected static string? GetValue(IReadOnlyDictionary<string, string?> values, string field) =>
        values.TryGetValue(field, out var value) ? value : null;

    /// <summary>Crea una incidencia agregada de calidad (no asociada a un registro).</summary>
    protected RecordValidationIssueDto CreateQualityIssue(
        ValidationIssueType issueType,
        ValidationSeverity severity,
        string message,
        string recommendedAction,
        string? field = null,
        string? value = null) => RecordValidationIssueDto.Create(
            0,
            Layer,
            LayerDisplayName,
            issueType,
            severity,
            RecordDisposition.Accept,
            message,
            recommendedAction,
            field,
            value);
}

/// <summary>
/// Reglas genéricas para archivos que no corresponden a una capa oficial.
/// </summary>
internal sealed class GenericValidationRules : LayerValidationRules
{
    /// <inheritdoc />
    public override ShapefileLayer Layer => ShapefileLayer.Unrecognized;

    /// <inheritdoc />
    public override DuplicateRule DuplicateRule => DuplicateRule.None;

    /// <inheritdoc />
    public override void InspectRecord(IReadOnlyDictionary<string, string?> values)
    {
        // Sin reglas específicas para archivos no reconocidos.
    }

    /// <inheritdoc />
    public override IReadOnlyList<ValidationStatisticDto> BuildStatistics() => [];
}
