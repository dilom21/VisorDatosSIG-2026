using System.ComponentModel.DataAnnotations;

namespace VisorDatosSIG.Application.DTOs.Empleados;

/// <summary>
/// Solicitud de alta de un nuevo empleado (CU04).
/// </summary>
public sealed record CreateEmpleadoRequestDto(
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(20, MinimumLength = 2, ErrorMessage = "El código debe tener entre 2 y 20 caracteres.")]
    string Codigo,

    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Los nombres deben tener entre 2 y 100 caracteres.")]
    string Nombres,

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Los apellidos deben tener entre 2 y 100 caracteres.")]
    string Apellidos,

    [Required(ErrorMessage = "El documento de identidad es obligatorio.")]
    [StringLength(30, MinimumLength = 4, ErrorMessage = "El documento de identidad debe tener entre 4 y 30 caracteres.")]
    string DocumentoIdentidad,

    [StringLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres.")]
    string? Telefono,

    [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    [StringLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
    string? Email,

    [Required(ErrorMessage = "El cargo es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El cargo debe tener entre 2 y 100 caracteres.")]
    string Cargo,

    [Required(ErrorMessage = "El área es obligatoria.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El área debe tener entre 2 y 100 caracteres.")]
    string Area,

    [Required(ErrorMessage = "La disponibilidad es obligatoria.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "La disponibilidad debe tener entre 2 y 50 caracteres.")]
    string Disponibilidad,

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    string? Observaciones);

/// <summary>
/// Solicitud de actualización de datos de un empleado existente (CU04).
/// El código del empleado se conserva para mantener trazabilidad histórica.
/// </summary>
public sealed record UpdateEmpleadoRequestDto(
    [Required(ErrorMessage = "Los nombres son obligatorios.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Los nombres deben tener entre 2 y 100 caracteres.")]
    string Nombres,

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Los apellidos deben tener entre 2 y 100 caracteres.")]
    string Apellidos,

    [Required(ErrorMessage = "El documento de identidad es obligatorio.")]
    [StringLength(30, MinimumLength = 4, ErrorMessage = "El documento de identidad debe tener entre 4 y 30 caracteres.")]
    string DocumentoIdentidad,

    [StringLength(30, ErrorMessage = "El teléfono no puede superar los 30 caracteres.")]
    string? Telefono,

    [EmailAddress(ErrorMessage = "El correo electrónico no tiene un formato válido.")]
    [StringLength(150, ErrorMessage = "El correo no puede superar los 150 caracteres.")]
    string? Email,

    [Required(ErrorMessage = "El cargo es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El cargo debe tener entre 2 y 100 caracteres.")]
    string Cargo,

    [Required(ErrorMessage = "El área es obligatoria.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El área debe tener entre 2 y 100 caracteres.")]
    string Area,

    [Required(ErrorMessage = "La disponibilidad es obligatoria.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "La disponibilidad debe tener entre 2 y 50 caracteres.")]
    string Disponibilidad,

    [StringLength(500, ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    string? Observaciones);

/// <summary>
/// Respuesta pública con los datos del empleado (sin datos sensibles).
/// </summary>
public sealed record EmpleadoResponseDto(
    int IdEmpleado,
    string Codigo,
    string Nombres,
    string Apellidos,
    string NombreCompleto,
    string DocumentoIdentidad,
    string? Telefono,
    string? Email,
    string Cargo,
    string Area,
    string Disponibilidad,
    bool Activo,
    DateTime FechaRegistro,
    DateTime? FechaModificacion,
    string? Observaciones);

/// <summary>
/// Solicitud de cambio de estado (activo/inactivo, baja lógica).
/// </summary>
public sealed record ChangeEmpleadoEstadoDto(bool Activo);

/// <summary>
/// Solicitud de cambio de disponibilidad operativa (Disponible, Asignado, Baja Parcial, etc.).
/// </summary>
public sealed record ChangeEmpleadoDisponibilidadDto(
    [Required(ErrorMessage = "El estado de disponibilidad es obligatorio.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "El estado de disponibilidad debe tener entre 2 y 50 caracteres.")]
    string Disponibilidad,

    [StringLength(500, ErrorMessage = "El motivo no puede superar los 500 caracteres.")]
    string? Motivo);

/// <summary>
/// Métricas resumidas para el panel de control del módulo de empleados.
/// </summary>
public sealed record EmpleadoMetricasDto(
    int Total,
    int Activos,
    int Inactivos,
    int Disponibles,
    int EnServicio,
    int BajasParciales,
    int Otros);
