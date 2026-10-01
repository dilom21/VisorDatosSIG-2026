using VisorDatosSIG.Application.DTOs.Navigation;

namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato de lectura del menú del sistema almacenado en <c>dbo.MenuOpciones</c>.
/// </summary>
public interface IMenuRepository
{
    /// <summary>
    /// Obtiene las opciones de menú activas (<c>Estado = 1</c>) en formato plano.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Filas planas del menú; la jerarquía se construye en la capa de servicio.</returns>
    Task<IReadOnlyList<MenuOpcionPlanaDto>> ObtenerOpcionesAsync(CancellationToken cancellationToken = default);
}
