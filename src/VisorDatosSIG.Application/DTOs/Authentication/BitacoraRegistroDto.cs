using VisorDatosSIG.Application.Common;

namespace VisorDatosSIG.Application.DTOs.Authentication;

/// <summary>
/// Valores utilizados en la bitácora para los eventos de autenticación.
/// </summary>
public static class BitacoraEventos
{
    /// <summary>Módulo oficial: Usuarios y Seguridad.</summary>
    public const string ModuloAutenticacion = ModulosSistema.UsuariosYSeguridad;

    /// <summary>Acción de inicio de sesión (CU01 - Gestionar Acceso al Sistema).</summary>
    public const string AccionInicioSesion = "Inicio de Sesión";

    /// <summary>Acción de cierre de sesión (CU01 - Gestionar Acceso al Sistema).</summary>
    public const string AccionCierreSesion = "Cierre de Sesión";

    /// <summary>Resultado exitoso.</summary>
    public const string ResultadoExitoso = "EXITOSO";

    /// <summary>Resultado fallido.</summary>
    public const string ResultadoFallido = "FALLIDO";

    /// <summary>Entidad afectada por los eventos de autenticación.</summary>
    public const string EntidadUsuario = "Usuarios";
}

/// <summary>
/// Registro a insertar en dbo.Bitacora.
/// </summary>
/// <remarks>
/// Nunca debe contener contraseñas, hashes, salts ni tokens.
/// </remarks>
public sealed class BitacoraRegistroDto
{
    /// <summary>Usuario involucrado; puede ser <c>null</c> cuando el login no existe.</summary>
    public int? IdUsuario { get; init; }

    /// <summary>Módulo del sistema.</summary>
    public required string Modulo { get; init; }

    /// <summary>Acción realizada.</summary>
    public required string Accion { get; init; }

    /// <summary>Resultado de la acción.</summary>
    public required string Resultado { get; init; }

    /// <summary>Entidad afectada, si corresponde.</summary>
    public string? Entidad { get; init; }

    /// <summary>Identificador de la entidad afectada, si corresponde.</summary>
    public long? IdEntidad { get; init; }

    /// <summary>Detalle legible, sin datos sensibles.</summary>
    public string? Detalle { get; init; }

    /// <summary>Dirección IP de origen, si fue posible determinarla.</summary>
    public string? Ip { get; init; }
}
