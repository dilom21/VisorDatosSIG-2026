namespace VisorDatosSIG.Application.DTOs;

/// <summary>
/// Disposición prevista del registro en una futura migración a SQL Server.
/// </summary>
public enum RecordDisposition
{
    /// <summary>El registro puede migrarse sin observaciones.</summary>
    Accept = 1,

    /// <summary>El registro puede migrarse, pero quedó registrada alguna advertencia.</summary>
    AcceptWithWarning = 2,

    /// <summary>El registro no debe migrarse (por ejemplo, geometría inválida o duplicado exacto).</summary>
    Omit = 3
}

/// <summary>
/// Textos visibles de la disposición del registro.
/// </summary>
public static class RecordDispositionExtensions
{
    /// <summary>
    /// Devuelve el texto de la disposición para mostrarlo al usuario.
    /// </summary>
    public static string ToDisplayName(this RecordDisposition disposition) => disposition switch
    {
        RecordDisposition.Accept => "Aceptar",
        RecordDisposition.AcceptWithWarning => "Aceptar con advertencia",
        _ => "Omitir"
    };
}
