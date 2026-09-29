namespace VisorDatosSIG.Application.DTOs.Authentication;

/// <summary>
/// Token de acceso generado junto con su vigencia.
/// </summary>
public sealed class TokenGeneradoDto
{
    /// <summary>Token JWT firmado.</summary>
    public required string AccessToken { get; init; }

    /// <summary>Vigencia del token expresada en segundos.</summary>
    public required int ExpiraEnSegundos { get; init; }
}
