using VisorDatosSIG.Application.DTOs.Bitacora;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato del servicio de consulta de la bitácora del sistema web (CU05).
/// </summary>
/// <remarks>
/// El servicio normaliza y acota los parámetros de entrada (página, tamaño, longitud de los
/// textos) antes de delegar en el repositorio, de modo que ninguna consulta llegue a SQL Server
/// con valores fuera de rango.
/// </remarks>
public interface IBitacoraConsultaService
{
    /// <summary>
    /// Consulta la bitácora aplicando filtros y paginación.
    /// </summary>
    /// <param name="consulta">Filtros y paginación solicitados por el cliente.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<BitacoraPaginaDto> ConsultarAsync(
        BitacoraConsultaDto consulta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los catálogos de filtros disponibles.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<BitacoraCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un evento de bitácora por identificador.
    /// </summary>
    /// <param name="idBitacora">Identificador del evento.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El evento; <c>null</c> cuando no existe o es un evento del migrador.</returns>
    Task<BitacoraItemWebDto?> ObtenerPorIdAsync(
        long idBitacora,
        CancellationToken cancellationToken = default);
}
