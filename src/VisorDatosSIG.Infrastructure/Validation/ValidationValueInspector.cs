using System.Globalization;

namespace VisorDatosSIG.Infrastructure.Validation;

/// <summary>
/// Utilidades para interpretar los valores de los atributos del DBF durante la validación.
/// </summary>
/// <remarks>
/// Los valores se comparan sin espacios extremos y sin modificar los datos originales:
/// el proyecto conserva los valores tal como provienen del archivo.
/// </remarks>
internal static class ValidationValueInspector
{
    /// <summary>
    /// Indica si el valor está ausente o vacío (se interpreta como NULL del diagnóstico).
    /// </summary>
    public static bool IsBlank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Indica si el valor es un número igual a cero.
    /// </summary>
    public static bool IsNumericZero(string? value)
    {
        if (IsBlank(value))
        {
            return false;
        }

        return double.TryParse(value!.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
               && number == 0d;
    }

    /// <summary>
    /// Compara dos valores ignorando espacios extremos. Dos valores vacíos se consideran equivalentes.
    /// </summary>
    public static bool AreEquivalent(string? first, string? second)
    {
        if (IsBlank(first) && IsBlank(second))
        {
            return true;
        }

        return string.Equals(first?.Trim(), second?.Trim(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Devuelve el valor listo para mostrarlo en un mensaje.
    /// </summary>
    public static string Describe(string? value) => IsBlank(value) ? "(nulo)" : value!.Trim();
}
