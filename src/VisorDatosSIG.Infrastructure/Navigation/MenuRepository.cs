using System.Data;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.DTOs.Navigation;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Data;

namespace VisorDatosSIG.Infrastructure.Navigation;

/// <summary>
/// Lectura del menú lateral del sistema desde <c>dbo.MenuOpciones</c> en SQL Server.
/// </summary>
/// <remarks>
/// La consulta es fija (no se concatena ningún valor) y filtra en el origen las
/// opciones inactivas (<c>Estado = 1</c>). El orden definitivo de padres e hijos
/// lo garantiza <see cref="MenuService"/> con <c>Orden</c> e <c>IdMenu</c>, de modo
/// que la jerarquía nunca depende del texto de <c>NombreMenu</c> ni de
/// identificadores fijos.
/// </remarks>
public sealed class MenuRepository : IMenuRepository
{
    private const string ConsultaMenu = """
        SELECT m.IdMenu,
               m.IdMenuPadre,
               m.NombreMenu,
               m.Url,
               m.Icono,
               m.Orden,
               m.Estado
        FROM dbo.MenuOpciones AS m
        WHERE m.Estado = 1
        ORDER BY m.Orden, m.IdMenu;
        """;

    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Inicializa el repositorio con el proveedor de conexiones a la base de datos.
    /// </summary>
    public MenuRepository(SqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task<IReadOnlyList<MenuOpcionPlanaDto>> ObtenerOpcionesAsync(
        CancellationToken cancellationToken = default)
    {
        var opciones = new List<MenuOpcionPlanaDto>();

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(ConsultaMenu, conexion);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        var ordinalId = lector.GetOrdinal("IdMenu");
        var ordinalPadre = lector.GetOrdinal("IdMenuPadre");
        var ordinalNombre = lector.GetOrdinal("NombreMenu");
        var ordinalUrl = lector.GetOrdinal("Url");
        var ordinalIcono = lector.GetOrdinal("Icono");
        var ordinalOrden = lector.GetOrdinal("Orden");
        var ordinalEstado = lector.GetOrdinal("Estado");

        while (await lector.ReadAsync(cancellationToken))
        {
            opciones.Add(new MenuOpcionPlanaDto
            {
                IdMenu = lector.GetInt32(ordinalId),
                IdMenuPadre = lector.IsDBNull(ordinalPadre) ? null : lector.GetInt32(ordinalPadre),
                NombreMenu = lector.IsDBNull(ordinalNombre) ? string.Empty : lector.GetString(ordinalNombre),
                Url = lector.IsDBNull(ordinalUrl) ? null : lector.GetString(ordinalUrl),
                Icono = lector.IsDBNull(ordinalIcono) ? null : lector.GetString(ordinalIcono),
                Orden = lector.IsDBNull(ordinalOrden) ? 0 : lector.GetInt32(ordinalOrden),
                // Estado es bit (1/0) en dbo.MenuOpciones; se normaliza a int para no
                // acoplar el DTO al tipo físico de la columna.
                Estado = lector.IsDBNull(ordinalEstado)
                    ? 0
                    : Convert.ToInt32(lector.GetValue(ordinalEstado), System.Globalization.CultureInfo.InvariantCulture)
            });
        }

        return opciones;
    }
}
