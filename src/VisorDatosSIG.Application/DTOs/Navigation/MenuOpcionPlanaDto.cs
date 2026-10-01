namespace VisorDatosSIG.Application.DTOs.Navigation;

/// <summary>
/// Fila plana de <c>dbo.MenuOpciones</c> tal como se lee de SQL Server.
/// </summary>
/// <remarks>
/// Es un DTO interno del backend: alimenta la construcción de la jerarquía y no se
/// expone al navegador. Incluye <see cref="IdMenuPadre"/> y <see cref="Estado"/>
/// porque la relación padre/hijo se resuelve por identificador, nunca por nombre.
/// </remarks>
public sealed class MenuOpcionPlanaDto
{
    /// <summary>Identificador de la opción de menú.</summary>
    public int IdMenu { get; init; }

    /// <summary>Identificador del menú padre; <c>null</c> cuando es un menú de primer nivel.</summary>
    public int? IdMenuPadre { get; init; }

    /// <summary>Nombre visible de la opción.</summary>
    public string NombreMenu { get; init; } = string.Empty;

    /// <summary>Ruta de destino; <c>null</c> cuando la opción es solo un agrupador.</summary>
    public string? Url { get; init; }

    /// <summary>Clave del icono del catálogo del frontend; <c>null</c> cuando no tiene icono.</summary>
    public string? Icono { get; init; }

    /// <summary>Orden de presentación dentro de su nivel.</summary>
    public int Orden { get; init; }

    /// <summary>Estado lógico: <c>1</c> activo, cualquier otro valor inactivo.</summary>
    public int Estado { get; init; }
}
