using VisorDatosSIG.Application.DTOs.Navigation;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato del servicio que entrega el menú lateral dinámico del sistema.
/// </summary>
/// <remarks>
/// El menú se filtra con los permisos efectivos del usuario autenticado (CU04 - Gestionar
/// Permisos), que se leen de la base de datos en cada petición:
/// <list type="bullet">
/// <item>Solo se incluyen las opciones con <c>Url</c> que alguno de los roles activos del
/// usuario puede ver.</item>
/// <item>Un agrupador se incluye únicamente si le queda al menos una opción visible, de modo que
/// nunca aparezca un menú vacío.</item>
/// <item>El rol <c>Administrador</c> recibe todas las opciones activas (acceso total implícito)
/// y el rol del migrador se ignora: un usuario que solo tenga ese rol no accede al visor web.</item>
/// </list>
/// </remarks>
public interface IMenuService
{
    /// <summary>
    /// Obtiene el menú jerárquico (padres con sus hijos) que corresponde al usuario.
    /// </summary>
    /// <param name="idUsuario">Identificador del usuario autenticado, tomado del token JWT.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>
    /// Opciones visibles y la marca de acceso web. Cuando
    /// <see cref="MenuUsuarioDto.TieneAccesoWeb"/> es <c>false</c> el usuario no puede usar la
    /// aplicación web y el controlador responde <c>403</c>.
    /// </returns>
    Task<MenuUsuarioDto> ObtenerMenuAsync(int idUsuario, CancellationToken cancellationToken = default);
}
