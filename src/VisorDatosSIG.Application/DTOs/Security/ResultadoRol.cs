namespace VisorDatosSIG.Application.DTOs.Security;

/// <summary>
/// Resultado de una operación de administración de roles (CU03/CU04).
/// </summary>
/// <remarks>
/// El servicio no lanza excepciones de negocio: devuelve el resultado y el controlador lo
/// traduce al código HTTP correspondiente (200/201, 400, 404 o 409).
/// </remarks>
public enum ResultadoRol
{
    /// <summary>Operación aplicada correctamente.</summary>
    Ok = 0,

    /// <summary>El rol no existe o no es administrable desde el módulo web.</summary>
    NoEncontrado = 1,

    /// <summary>Ya existe otro rol con el mismo nombre.</summary>
    NombreDuplicado = 2,

    /// <summary>El nombre está reservado por el sistema.</summary>
    NombreReservado = 3,

    /// <summary>El rol está protegido por el sistema (por ejemplo <c>Administrador</c>).</summary>
    RolProtegido = 4,

    /// <summary>El rol tiene usuarios activos asignados y no puede deshabilitarse.</summary>
    RolEnUso = 5,

    /// <summary>Los datos enviados no son válidos (formato, longitudes o menús inexistentes).</summary>
    DatosInvalidos = 6
}

/// <summary>
/// Resultado de una operación que devuelve un rol.
/// </summary>
public sealed class RolOperacionDto
{
    /// <summary>Desenlace de la operación.</summary>
    public ResultadoRol Estado { get; init; }

    /// <summary>Explicación legible para el usuario; <c>null</c> cuando la operación fue exitosa.</summary>
    public string? Detalle { get; init; }

    /// <summary>Rol resultante; solo se informa cuando <see cref="Estado"/> es <see cref="ResultadoRol.Ok"/>.</summary>
    public RolDto? Rol { get; init; }

    /// <summary>Indica si la operación se completó.</summary>
    public bool IsSuccess => Estado == ResultadoRol.Ok;

    /// <summary>Crea un resultado exitoso con el rol resultante.</summary>
    public static RolOperacionDto Exitoso(RolDto rol) => new() { Estado = ResultadoRol.Ok, Rol = rol };

    /// <summary>Crea un resultado fallido con el motivo.</summary>
    public static RolOperacionDto Fallido(ResultadoRol estado, string detalle) =>
        new() { Estado = estado, Detalle = detalle };
}

/// <summary>
/// Resultado de guardar la matriz de permisos de un rol (CU04 - Gestionar Permisos).
/// </summary>
public sealed class RolPermisosOperacionDto
{
    /// <summary>Desenlace de la operación.</summary>
    public ResultadoRol Estado { get; init; }

    /// <summary>Explicación legible para el usuario; <c>null</c> cuando la operación fue exitosa.</summary>
    public string? Detalle { get; init; }

    /// <summary>Matriz resultante; solo se informa cuando la operación se completó.</summary>
    public RolPermisosDto? Permisos { get; init; }

    /// <summary>Indica si la operación se completó.</summary>
    public bool IsSuccess => Estado == ResultadoRol.Ok;

    /// <summary>Crea un resultado exitoso con la matriz vigente.</summary>
    public static RolPermisosOperacionDto Exitoso(RolPermisosDto permisos) =>
        new() { Estado = ResultadoRol.Ok, Permisos = permisos };

    /// <summary>Crea un resultado fallido con el motivo.</summary>
    public static RolPermisosOperacionDto Fallido(ResultadoRol estado, string detalle) =>
        new() { Estado = estado, Detalle = detalle };
}
