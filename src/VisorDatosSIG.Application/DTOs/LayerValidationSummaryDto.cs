namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Resumen de la validación de una capa: cuántos registros se analizaron
/// y cómo se clasifican de cara a una futura migración.
/// </summary>
public sealed class LayerValidationSummaryDto
{
    /// <summary>Capa validada.</summary>
    public required ShapefileLayer Layer { get; init; }

    /// <summary>Nombre visible de la capa.</summary>
    public required string LayerDisplayName { get; init; }

    /// <summary>Cantidad de registros analizados.</summary>
    public int AnalyzedRecords { get; init; }

    /// <summary>Registros válidos sin ninguna incidencia.</summary>
    public int ValidRecords { get; init; }

    /// <summary>Registros válidos que tienen advertencias (disposición AcceptWithWarning).</summary>
    public int RecordsWithWarnings { get; init; }

    /// <summary>Registros que deben omitirse en una futura migración (disposición Omit).</summary>
    public int OmitableRecords { get; init; }

    /// <summary>Cantidad de incidencias con severidad Error.</summary>
    public int ErrorIssueCount { get; init; }

    /// <summary>Cantidad de incidencias con severidad Warning.</summary>
    public int WarningIssueCount { get; init; }

    /// <summary>Cantidad de incidencias con severidad Info.</summary>
    public int InfoIssueCount { get; init; }

    /// <summary>Cantidad total de incidencias registradas.</summary>
    public int TotalIssueCount => ErrorIssueCount + WarningIssueCount + InfoIssueCount;

    /// <summary>Registros candidatos a migración (analizados menos omitibles).</summary>
    public int MigrationCandidateRecords => Math.Max(0, AnalyzedRecords - OmitableRecords);

    /// <summary>
    /// Crea un resumen vacío, utilizado cuando la validación no pudo ejecutarse.
    /// </summary>
    public static LayerValidationSummaryDto CreateEmpty(ShapefileLayer layer, string layerDisplayName) => new()
    {
        Layer = layer,
        LayerDisplayName = layerDisplayName
    };
}
