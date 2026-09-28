namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Información de progreso de la validación detallada.
/// </summary>
public sealed class ValidationProgressDto
{
    /// <summary>Etapa en curso (por ejemplo: "Analizando registros").</summary>
    public required string Phase { get; init; }

    /// <summary>Registros procesados hasta el momento.</summary>
    public required int ProcessedRecords { get; init; }

    /// <summary>Cantidad total de registros a procesar.</summary>
    public required int TotalRecords { get; init; }

    /// <summary>Porcentaje de avance (0 a 100).</summary>
    public int Percentage => TotalRecords <= 0
        ? 0
        : Math.Clamp((int)Math.Round(ProcessedRecords * 100d / TotalRecords), 0, 100);
}
