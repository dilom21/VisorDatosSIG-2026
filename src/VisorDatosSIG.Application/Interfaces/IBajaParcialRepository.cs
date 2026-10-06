using VisorDatosSIG.Application.DTOs.BajasParciales;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Acceso a datos para CU18 - Gestionar Bajas Parciales.
/// </summary>
public interface IBajaParcialRepository
{
    Task<IReadOnlyList<BajaParcialEmpleadoCatalogoDto>> ObtenerEmpleadosCatalogoAsync(
        CancellationToken ct = default);

    Task<IEnumerable<BajaParcialResponseDto>> ObtenerTodosAsync(
        int? idEmpleado = null,
        string? estado = null,
        CancellationToken ct = default);

    Task<BajaParcialResponseDto?> ObtenerPorIdAsync(
        int idBajaParcial,
        CancellationToken ct = default);

    Task<BajaParcialResponseDto?> CrearAsync(
        CreateBajaParcialRequestDto dto,
        CancellationToken ct = default);

    Task<BajaParcialResponseDto?> ActualizarAsync(
        int idBajaParcial,
        UpdateBajaParcialRequestDto dto,
        CancellationToken ct = default);

    Task<bool> CambiarEstadoAsync(
        int idBajaParcial,
        string estado,
        CancellationToken ct = default);

    Task<bool> ExisteSolapamientoAsync(
        int idEmpleado,
        DateTime fechaInicio,
        DateTime fechaFin,
        int? ignorarIdBajaParcial = null,
        CancellationToken ct = default);

    Task<int> SincronizarVigenciasAsync(
    DateTime fechaReferencia,
    CancellationToken ct = default);
}
