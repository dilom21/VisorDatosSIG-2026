using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Disponibilidad;

namespace VisorDatosSIG.Application.Interfaces;

public interface IDisponibilidadRepository
{
    Task<IReadOnlyList<HorarioTrabajoDto>?> ObtenerHorariosAsync(int idEmpleado, CancellationToken ct = default);
    Task<ResultadoOperacion<HorarioTrabajoDto>> CrearHorarioAsync(int idEmpleado, GuardarHorarioRequestDto dto, CancellationToken ct = default);
    Task<ResultadoOperacion<HorarioTrabajoDto>> ActualizarHorarioAsync(int idHorario, GuardarHorarioRequestDto dto, CancellationToken ct = default);
    Task<ResultadoOperacion<HorarioTrabajoDto>> CambiarEstadoHorarioAsync(int idHorario, bool activo, CancellationToken ct = default);
    Task<IReadOnlyList<DisponibilidadEmpleadoDto>> ConsultarDisponibilidadAsync(DisponibilidadConsultaDto consulta, CancellationToken ct = default);
    Task<PagedResult<AsignacionTrabajoResumenDto>> ObtenerAsignacionesAsync(AsignacionTrabajoConsultaDto consulta, CancellationToken ct = default);
    Task<AsignacionTrabajoDetalleDto?> ObtenerAsignacionAsync(int idAsignacion, CancellationToken ct = default);
    Task<ResultadoOperacion<AsignacionTrabajoDetalleDto>> CrearAsignacionAsync(GuardarAsignacionTrabajoDto dto, CancellationToken ct = default);
    Task<ResultadoOperacion<AsignacionTrabajoDetalleDto>> ActualizarAsignacionAsync(int idAsignacion, GuardarAsignacionTrabajoDto dto, CancellationToken ct = default);
    Task<ResultadoOperacion<AsignacionTrabajoDetalleDto>> CambiarEstadoAsignacionAsync(int idAsignacion, string estado, CancellationToken ct = default);
}
