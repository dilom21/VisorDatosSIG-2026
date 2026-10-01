using System.ComponentModel.DataAnnotations;

namespace VisorDatosSIG.Application.DTOs.Security;

/// <summary>
/// Cuerpo de <c>PUT /api/roles/{id}/permisos</c>: la matriz completa de permisos del rol.
/// </summary>
/// <remarks>
/// El reemplazo es total e idempotente: el servicio elimina las filas actuales de
/// <c>dbo.RolMenu</c> del rol dentro de una transacción e inserta lo recibido, de modo que
/// repetir la misma petición deja exactamente el mismo estado.
/// </remarks>
public sealed class RolPermisosSolicitudDto
{
    /// <summary>Filas de la matriz; si viene vacía, el rol queda sin ningún permiso.</summary>
    [Required(ErrorMessage = "La lista de permisos es obligatoria.")]
    public IReadOnlyList<PermisoMenuSolicitudDto> Permisos { get; set; } = [];
}

/// <summary>
/// Permiso solicitado para un menú concreto.
/// </summary>
/// <remarks>
/// Cada permiso es independiente: se persiste exactamente lo enviado. Las filas sin ningún
/// permiso se omiten porque su ausencia en <c>dbo.RolMenu</c> significa lo mismo.
/// </remarks>
public sealed class PermisoMenuSolicitudDto
{
    /// <summary>Identificador de la opción de menú.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "El identificador de menú debe ser mayor que cero.")]
    public int IdMenu { get; set; }

    /// <summary>Otorga el permiso de ver.</summary>
    public bool PuedeVer { get; set; }

    /// <summary>Otorga el permiso de crear.</summary>
    public bool PuedeCrear { get; set; }

    /// <summary>Otorga el permiso de editar.</summary>
    public bool PuedeEditar { get; set; }

    /// <summary>Otorga el permiso de eliminar.</summary>
    public bool PuedeEliminar { get; set; }
}
