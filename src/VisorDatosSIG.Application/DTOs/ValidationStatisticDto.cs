using System.Globalization;

namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Estadística de calidad obtenida durante la validación detallada.
/// Se utilizan para informar situaciones normales o repetitivas sin generar
/// miles de incidencias individuales.
/// </summary>
public sealed class ValidationStatisticDto
{
    /// <summary>Identificador estable de la estadística (para pruebas y trazabilidad).</summary>
    public required string Key { get; init; }

    /// <summary>Etiqueta visible de la estadística.</summary>
    public required string Label { get; init; }

    /// <summary>Valor ya formateado para mostrar.</summary>
    public required string Value { get; init; }

    /// <summary>Severidad asociada (sirve para colorear el resumen).</summary>
    public ValidationSeverity Severity { get; init; } = ValidationSeverity.Info;

    /// <summary>Nota aclaratoria opcional.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Crea una estadística a partir de un conteo de registros.
    /// </summary>
    public static ValidationStatisticDto FromCount(
        string key,
        string label,
        long count,
        ValidationSeverity severity = ValidationSeverity.Info,
        string? note = null) => new()
    {
        Key = key,
        Label = label,
        Value = count.ToString("N0", CultureInfo.CurrentCulture),
        Severity = severity,
        Note = note
    };

    /// <summary>
    /// Crea una estadística con un valor de texto libre.
    /// </summary>
    public static ValidationStatisticDto FromText(
        string key,
        string label,
        string value,
        ValidationSeverity severity = ValidationSeverity.Info,
        string? note = null) => new()
    {
        Key = key,
        Label = label,
        Value = value,
        Severity = severity,
        Note = note
    };
}
