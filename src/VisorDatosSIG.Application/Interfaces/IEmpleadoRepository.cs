using VisorDatosSIG.Application.DTOs.Empleados;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato de acceso a datos para la gestión de empleados (CU04).
/// </summary>
public interface IEmpleadoRepository
{
    /// <summary>
    /// Lista los empleados con filtros opcionales de búsqueda, cargo, disponibilidad y estado.
    /// </summary>
    Task<IEnumerable<EmpleadoResponseDto>> ObtenerTodosAsync(
        string? busqueda = null,
        string? cargo = null,
        string? disponibilidad = null,
        bool? activo = null,
        CancellationToken ct = default);

    /// <summary>
    /// Obtiene un empleado por su identificador único.
    /// </summary>
    Task<EmpleadoResponseDto?> ObtenerPorIdAsync(int idEmpleado, CancellationToken ct = default);

    /// <summary>
    /// Obtiene un empleado por su código único.
    /// </summary>
    Task<EmpleadoResponseDto?> ObtenerPorCodigoAsync(string codigo, CancellationToken ct = default);

    /// <summary>
    /// Registra un nuevo empleado.
    /// </summary>
    Task<EmpleadoResponseDto?> CrearAsync(CreateEmpleadoRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Actualiza la información de un empleado existente.
    /// </summary>
    Task<EmpleadoResponseDto?> ActualizarAsync(int idEmpleado, UpdateEmpleadoRequestDto dto, CancellationToken ct = default);

    /// <summary>
    /// Activa o desactiva (baja lógica) a un empleado.
    /// </summary>
    Task<bool> CambiarEstadoAsync(int idEmpleado, bool activo, CancellationToken ct = default);

    /// <summary>
    /// Actualiza la disponibilidad operativa del empleado.
    /// </summary>
    Task<bool> CambiarDisponibilidadAsync(int idEmpleado, string disponibilidad, string? motivo = null, CancellationToken ct = default);

    /// <summary>
    /// Verifica si un código ya se encuentra registrado.
    /// </summary>
    Task<bool> ExisteCodigoAsync(string codigo, int? ignorarId = null, CancellationToken ct = default);

    /// <summary>
    /// Verifica si un documento de identidad ya se encuentra registrado.
    /// </summary>
    Task<bool> ExisteDocumentoAsync(string documento, int? ignorarId = null, CancellationToken ct = default);

    /// <summary>
    /// Obtiene los cargos distintos registrados en el sistema.
    /// </summary>
    Task<IReadOnlyList<string>> ObtenerCargosAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene los estados de disponibilidad distintos registrados.
    /// </summary>
    Task<IReadOnlyList<string>> ObtenerDisponibilidadesAsync(CancellationToken ct = default);

    /// <summary>
    /// Obtiene las métricas numéricas agregadas del personal operativo.
    /// </summary>
    Task<EmpleadoMetricasDto> ObtenerMetricasAsync(CancellationToken ct = default);
}
