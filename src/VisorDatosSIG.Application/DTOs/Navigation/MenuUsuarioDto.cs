namespace VisorDatosSIG.Application.DTOs.Navigation;

/// <summary>
/// Menú lateral que corresponde a un usuario autenticado, junto con su derecho de acceso web.
/// </summary>
/// <remarks>
/// Es un modelo de salida del backend, no el cuerpo de la respuesta: <c>GET /api/menu</c> sigue
/// respondiendo el arreglo de <see cref="MenuOpcionDto"/> (contrato ya publicado) y usa
/// <see cref="TieneAccesoWeb"/> para decidir entre <c>200</c> y <c>403</c>. De este modo el
/// filtrado por permisos no cambia la forma del JSON que consume el visor.
/// </remarks>
public sealed class MenuUsuarioDto
{
    /// <summary>
    /// Indica si el usuario puede usar la aplicación web (tiene al menos un rol web activo).
    /// </summary>
    public bool TieneAccesoWeb { get; init; }

    /// <summary>
    /// Opciones visibles para el usuario, ordenadas y anidadas. Está vacío cuando no tiene acceso
    /// web o cuando sus roles no otorgan ninguna opción.
    /// </summary>
    public IReadOnlyList<MenuOpcionDto> Opciones { get; init; } = [];

    /// <summary>Crea el resultado de un usuario sin acceso a la aplicación web.</summary>
    /// <returns>Resultado sin acceso y sin opciones.</returns>
    public static MenuUsuarioDto SinAccesoWeb() => new();
}
