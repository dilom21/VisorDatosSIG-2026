using System.ComponentModel.DataAnnotations;
using VisorDatosSIG.Application.Common;

namespace VisorDatosSIG.Application.DTOs.Security;

/// <summary>
/// Cuerpo de las peticiones de creación (<c>POST /api/roles</c>) y modificación
/// (<c>PATCH /api/roles/{id}</c>) de roles.
/// </summary>
/// <remarks>
/// Solo permite nombre y descripción: el estado lógico se cambia con
/// <c>PATCH /api/roles/{id}/estado</c> y los permisos con <c>PUT /api/roles/{id}/permisos</c>.
/// Las longitudes coinciden con las columnas <c>dbo.Roles.NombreRol</c> (50) y
/// <c>dbo.Roles.Descripcion</c> (200).
/// </remarks>
public sealed class RolSolicitudDto
{
    /// <summary>Nombre del rol (obligatorio, máximo 50 caracteres).</summary>
    [Required(ErrorMessage = "El nombre del rol es obligatorio.")]
    [StringLength(
        RolesSistema.LongitudMaximaNombre,
        MinimumLength = 1,
        ErrorMessage = "El nombre del rol no puede superar los 50 caracteres.")]
    public string NombreRol { get; set; } = string.Empty;

    /// <summary>Descripción funcional del rol (opcional, máximo 200 caracteres).</summary>
    [StringLength(
        RolesSistema.LongitudMaximaDescripcion,
        ErrorMessage = "La descripción no puede superar los 200 caracteres.")]
    public string? Descripcion { get; set; }
}

/// <summary>
/// Cuerpo de <c>PATCH /api/roles/{id}/estado</c>: habilitación o deshabilitación lógica.
/// </summary>
/// <remarks>
/// La API nunca elimina físicamente un rol: un rol con historial de permisos o usuarios
/// asignados se deshabilita, conservando la trazabilidad de la bitácora.
/// </remarks>
public sealed class RolEstadoSolicitudDto
{
    /// <summary>Estado deseado: <c>true</c> habilitar, <c>false</c> deshabilitar.</summary>
    public bool Activo { get; set; }
}
