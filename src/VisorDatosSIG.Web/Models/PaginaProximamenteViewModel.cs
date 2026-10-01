namespace VisorDatosSIG.Web.Models;

/// <summary>
/// Datos de una página "Próximamente": describe la funcionalidad que el usuario
/// seleccionó en el menú dinámico para una ruta cuya implementación aún no existe.
/// </summary>
/// <remarks>
/// El módulo se toma del catálogo oficial (<c>ModulosSistema</c>), no de literales
/// sueltos, y la ruta corresponde a la <c>Url</c> registrada en <c>dbo.MenuOpciones</c>.
/// Este modelo describe la PÁGINA; no alimenta el sidebar, que siempre se construye
/// con la respuesta de <c>GET /api/menu</c>.
/// </remarks>
public sealed class PaginaProximamenteViewModel
{
    /// <summary>Módulo del sistema al que pertenece la funcionalidad.</summary>
    public string Modulo { get; init; } = string.Empty;

    /// <summary>Nombre de la funcionalidad seleccionada en el menú.</summary>
    public string Funcionalidad { get; init; } = string.Empty;

    /// <summary>Descripción breve mostrada al usuario.</summary>
    public string Descripcion { get; init; } = "Este módulo estará disponible próximamente.";

    /// <summary>Ruta de la página; se usa como <c>returnUrl</c> del guard de sesión.</summary>
    public string RutaActual { get; init; } = "/";
}
