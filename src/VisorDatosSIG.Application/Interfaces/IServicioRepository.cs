using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Servicios;

namespace VisorDatosSIG.Application.Interfaces;

public interface IServicioRepository
{
    Task<PagedResult<ServicioDto>> ObtenerTodosAsync(
        string? busqueda = null,
        byte? estado = null,
        int pagina = 1,
        int limite = 20,
        CancellationToken ct = default);

    Task<ServicioDto?> ObtenerPorIdAsync(int idCodigo, CancellationToken ct = default);

    Task<CambioEstadoServicioDto?> CambiarEstadoAsync(
        int idCodigo,
        byte estadoNuevo,
        CancellationToken ct = default);

    Task<ServicioResumenDto> ObtenerResumenAsync(CancellationToken ct = default);
}
