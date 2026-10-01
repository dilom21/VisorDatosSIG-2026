using VisorDatosSIG.Application.DTOs.Bitacora;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato de consulta paginada de la bitácora del sistema web (CU05).
/// </summary>
/// <remarks>
/// La consulta es un servicio distinto del registro de eventos (<see cref="IBitacoraRepository"/>)
/// porque tiene un consumidor distinto: el módulo de auditoría de la versión web.
/// <list type="bullet">
/// <item>Los eventos del módulo "Migrador de Datos Geográficos" se excluyen <b>en SQL</b>, nunca
/// filtrando en memoria: la página siempre está completa respecto de lo que el usuario puede ver.</item>
/// <item>La paginación (<c>OFFSET/FETCH</c>) y los filtros se resuelven en SQL Server con
/// parámetros; el repositorio nunca concatena valores en el texto de la consulta.</item>
/// <item>El orden es determinista: <c>FechaHora DESC</c> e <c>IdBitacora DESC</c>.</item>
/// </list>
/// </remarks>
public interface IBitacoraConsultaRepository
{
    /// <summary>
    /// Consulta una página de eventos de bitácora según los filtros indicados.
    /// </summary>
    /// <param name="consulta">Filtros y paginación ya normalizados.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Página solicitada con el total de registros que cumplen los filtros.</returns>
    Task<BitacoraPaginaDto> ConsultarAsync(
        BitacoraConsultaDto consulta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los valores distintos disponibles para los filtros (sin los valores del migrador).
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    Task<BitacoraCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el detalle de un evento de bitácora.
    /// </summary>
    /// <param name="idBitacora">Identificador del evento.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>El evento; <c>null</c> cuando no existe o pertenece al migrador.</returns>
    Task<BitacoraItemWebDto?> ObtenerPorIdAsync(
        long idBitacora,
        CancellationToken cancellationToken = default);
}
