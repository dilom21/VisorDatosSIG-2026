using System.ComponentModel.DataAnnotations;

namespace VisorDatosSIG.Application.DTOs.BajasParciales;

/// <summary>
/// Solicitud para registrar una baja parcial temporal de un empleado (CU18).
/// </summary>
public sealed record CreateBajaParcialRequestDto(
    [Range(1, int.MaxValue, ErrorMessage = "El empleado es obligatorio.")]
    int IdEmpleado,

    DateTime FechaInicio,

    DateTime FechaFin,

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(300, MinimumLength = 3,
        ErrorMessage = "El motivo debe tener entre 3 y 300 caracteres.")]
    string Motivo,

    [StringLength(500,
        ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    string? Observaciones);

/// <summary>
/// Solicitud de modificación de una baja parcial existente.
/// El empleado asociado no se cambia para conservar la trazabilidad.
/// </summary>
public sealed record UpdateBajaParcialRequestDto(
    DateTime FechaInicio,

    DateTime FechaFin,

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(300, MinimumLength = 3,
        ErrorMessage = "El motivo debe tener entre 3 y 300 caracteres.")]
    string Motivo,

    [StringLength(500,
        ErrorMessage = "Las observaciones no pueden superar los 500 caracteres.")]
    string? Observaciones);

/// <summary>
/// Solicitud para cambiar el estado de una baja parcial.
/// </summary>
public sealed record ChangeBajaParcialEstadoDto(
    [Required(ErrorMessage = "El estado es obligatorio.")]
    [RegularExpression(
        "^(Activa|Finalizada|Cancelada)$",
        ErrorMessage = "El estado debe ser Activa, Finalizada o Cancelada.")]
    string Estado);

/// <summary>
/// Información pública de una baja parcial.
/// </summary>
public sealed record BajaParcialResponseDto(
    int IdBajaParcial,
    int IdEmpleado,
    string CodigoEmpleado,
    string NombreEmpleado,
    DateTime FechaInicio,
    DateTime FechaFin,
    string Motivo,
    string? Observaciones,
    string Estado,
    DateTime FechaRegistro,
    DateTime? FechaModificacion);

/// <summary>
/// Catálogo mínimo de empleados que pueden asociarse a una baja parcial.
/// </summary>
public sealed record BajaParcialEmpleadoCatalogoDto(
    int IdEmpleado,
    string Codigo,
    string NombreCompleto,
    string Disponibilidad,
    bool Activo);
