using VisorDatosSIG.Application.DTOs.Authentication;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato de registro de eventos en dbo.Bitacora.
/// </summary>
public interface IBitacoraRepository
{
    /// <summary>
    /// Registra un evento en la bitácora del sistema.
    /// </summary>
    /// <param name="registro">Datos del evento (sin información sensible).</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task RegistrarAsync(BitacoraRegistroDto registro, CancellationToken cancellationToken = default);
}
