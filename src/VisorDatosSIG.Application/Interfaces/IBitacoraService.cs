using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato para el registro persistente y consulta de auditoría y operaciones en la base de datos (RF-MIG-10, CU30).
/// </summary>
public interface IBitacoraService
{
    /// <summary>
    /// Inserta una entrada de bitácora en SQL Server de manera asíncrona.
    /// Si ocurre un fallo en el registro de auditoría, no debe interrumpir el flujo principal.
    /// </summary>
    Task<bool> RegistrarAsync(BitacoraEntryDto entrada, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta los últimos registros de la bitácora ordenados del más reciente al más antiguo.
    /// </summary>
    Task<IReadOnlyList<BitacoraItemDto>> ObtenerHistorialAsync(int limite = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta los registros de la bitácora permitiendo filtrar por módulo a incluir y/o módulo a excluir.
    /// </summary>
    Task<IReadOnlyList<BitacoraItemDto>> ObtenerHistorialAsync(string? modulo, string? moduloExcluido, int limite = 100, CancellationToken cancellationToken = default);
}
