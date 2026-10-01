using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Authentication;

namespace VisorDatosSIG.Application.DTOs.Security;

/// <summary>
/// Valores de bitácora de los eventos de administración de roles y permisos (CU03 y CU04).
/// </summary>
/// <remarks>
/// Se centralizan aquí, igual que <see cref="BitacoraEventos"/> hace con la autenticación, para
/// que el módulo de consulta de bitácora (CU05) muestre textos homogéneos y para que ninguna
/// cadena de auditoría quede escrita a mano dentro de un repositorio.
/// <para>
/// <c>Entidad</c> identifica la tabla afectada y <c>IdEntidad</c> el registro: en la matriz de
/// permisos se registra el identificador del <b>rol</b>, porque es el objeto que administra el
/// usuario (las filas de <c>dbo.RolMenu</c> son un detalle de su implementación).
/// </para>
/// </remarks>
public static class BitacoraEventosSeguridad
{
    /// <summary>Módulo oficial de los eventos de seguridad (Usuarios y Seguridad).</summary>
    public const string Modulo = ModulosSistema.UsuariosYSeguridad;

    /// <summary>Acción de alta de un rol (CU03 - Gestionar Roles).</summary>
    public const string AccionCrearRol = "Crear Rol";

    /// <summary>Acción de modificación de nombre o descripción de un rol (CU03 - Gestionar Roles).</summary>
    public const string AccionModificarRol = "Modificar Rol";

    /// <summary>Acción de habilitación o deshabilitación lógica de un rol (CU03 - Gestionar Roles).</summary>
    public const string AccionCambiarEstadoRol = "Cambiar Estado Rol";

    /// <summary>Acción de reemplazo de la matriz de permisos de un rol (CU04 - Gestionar Permisos).</summary>
    public const string AccionActualizarPermisos = "Actualizar Permisos Rol";

    /// <summary>Entidad de los eventos de roles.</summary>
    public const string EntidadRol = "Roles";

    /// <summary>Entidad de los eventos de la matriz de permisos por menú.</summary>
    public const string EntidadPermisoRol = "RolMenu";

    /// <summary>
    /// Construye el evento de bitácora de una operación de seguridad completada correctamente.
    /// </summary>
    /// <param name="contexto">Quién ejecuta la operación y desde qué IP; puede ser <c>null</c>.</param>
    /// <param name="accion">Acción de las constantes de esta clase.</param>
    /// <param name="entidad">Entidad afectada.</param>
    /// <param name="idEntidad">Identificador del registro afectado.</param>
    /// <param name="detalle">Detalle legible, sin datos sensibles.</param>
    /// <returns>Evento listo para persistirse junto con la operación, en su misma transacción.</returns>
    public static BitacoraRegistroDto Evento(
        ContextoAuditoriaDto? contexto,
        string accion,
        string entidad,
        int? idEntidad,
        string detalle) => new()
        {
            IdUsuario = contexto?.IdUsuario,
            Modulo = Modulo,
            Accion = accion,
            Resultado = BitacoraEventos.ResultadoExitoso,
            Entidad = entidad,
            IdEntidad = idEntidad,
            Detalle = detalle,
            Ip = contexto?.Ip
        };
}
