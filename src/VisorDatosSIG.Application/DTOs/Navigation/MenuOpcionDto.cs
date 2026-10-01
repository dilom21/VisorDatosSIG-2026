namespace VisorDatosSIG.Application.DTOs.Navigation;

/// <summary>
/// Opción del menú lateral dinámico del VisorDatosSIG, con sus opciones hijas.
/// </summary>
/// <remarks>
/// Contrato público del endpoint <c>GET /api/menu</c>. Solo expone lo que el
/// sidebar necesita para renderizar: identificador, nombre, ruta, icono, orden y
/// la colección de opciones hijas. No expone <c>IdMenuPadre</c>, <c>Nivel</c> ni
/// <c>Estado</c> porque la jerarquía ya viaja anidada.
/// </remarks>
public sealed class MenuOpcionDto
{
    /// <summary>Identificador de la opción de menú.</summary>
    public int IdMenu { get; init; }

    /// <summary>Nombre visible de la opción.</summary>
    public string NombreMenu { get; init; } = string.Empty;

    /// <summary>
    /// Ruta de destino. Es <c>null</c> cuando la opción es un agrupador
    /// (sus hijos son los elementos navegables).
    /// </summary>
    public string? Url { get; init; }

    /// <summary>Clave del icono dentro del catálogo controlado del frontend.</summary>
    public string? Icono { get; init; }

    /// <summary>Orden de presentación dentro de su nivel.</summary>
    public int Orden { get; init; }

    /// <summary>Opciones hijas ordenadas; colección vacía cuando la opción es un enlace.</summary>
    public IReadOnlyList<MenuOpcionDto> Opciones { get; init; } = [];
}
