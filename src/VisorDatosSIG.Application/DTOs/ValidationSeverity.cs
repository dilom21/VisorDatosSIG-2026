namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Severidad de una incidencia detectada durante la validación detallada.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>Observación informativa: no afecta la migración.</summary>
    Info = 1,

    /// <summary>Advertencia: el registro puede migrarse pero requiere atención.</summary>
    Warning = 2,

    /// <summary>Error: el registro no debe migrarse tal como está.</summary>
    Error = 3
}

/// <summary>
/// Textos visibles de la severidad.
/// </summary>
public static class ValidationSeverityExtensions
{
    /// <summary>
    /// Devuelve el texto de la severidad para mostrarlo al usuario.
    /// </summary>
    public static string ToDisplayName(this ValidationSeverity severity) => severity switch
    {
        ValidationSeverity.Info => "Información",
        ValidationSeverity.Warning => "Advertencia",
        _ => "Error"
    };
}
