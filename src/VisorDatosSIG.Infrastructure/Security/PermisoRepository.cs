using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.Common;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Data;

namespace VisorDatosSIG.Infrastructure.Security;

/// <summary>
/// Lectura de permisos desde <c>dbo.UsuariosRoles</c>, <c>dbo.Roles</c>, <c>dbo.RolMenu</c> y
/// <c>dbo.MenuOpciones</c>.
/// </summary>
/// <remarks>
/// Todas las consultas son parametrizadas: el único texto que se compone es la lista de
/// marcadores (<c>@IdRol0</c>, <c>@IdRol1</c>, ...) según la cantidad de roles del usuario; los
/// valores nunca se concatenan.
/// <para>
/// Los menús se filtran por <c>MenuOpciones.Estado = 1</c>: una opción dada de baja no autoriza
/// nada. La comparación de la ruta usa la collation de la base
/// (<c>SQL_Latin1_General_CP1_CI_AS</c>), por lo que no distingue mayúsculas.
/// </para>
/// </remarks>
public sealed class PermisoRepository : IPermisoRepository
{
    /// <summary>
    /// Roles activos del usuario. El usuario debe estar activo (<c>dbo.Usuarios.Activo = 1</c>):
    /// si un administrador lo deshabilita, deja de resolver permisos en la siguiente petición
    /// aunque su token siga vigente.
    /// </summary>
    private const string ConsultaRolesActivos = """
        SELECT r.IdRol,
               r.NombreRol
        FROM dbo.UsuariosRoles AS ur
        INNER JOIN dbo.Usuarios AS u ON u.IdUsuario = ur.IdUsuario
        INNER JOIN dbo.Roles AS r ON r.IdRol = ur.IdRol
        WHERE ur.IdUsuario = @IdUsuario
          AND u.Activo = 1
          AND r.Estado = 1
        ORDER BY r.NombreRol;
        """;

    /// <summary>Permiso puntual de una acción sobre una ruta. <c>{0}</c>: marcadores de rol.</summary>
    private const string ConsultaTienePermiso = """
        SELECT TOP (1) 1
        FROM dbo.RolMenu AS rm
        INNER JOIN dbo.MenuOpciones AS m ON m.IdMenu = rm.IdMenu
        WHERE m.Estado = 1
          AND m.Url = @Url
          AND rm.IdRol IN ({0})
          AND (CASE @Accion
                   WHEN 1 THEN rm.PuedeVer
                   WHEN 2 THEN rm.PuedeCrear
                   WHEN 3 THEN rm.PuedeEditar
                   ELSE rm.PuedeEliminar
               END) = 1;
        """;

    /// <summary>Menús visibles. <c>{0}</c>: marcadores de rol.</summary>
    private const string ConsultaMenusVisibles = """
        SELECT DISTINCT rm.IdMenu
        FROM dbo.RolMenu AS rm
        INNER JOIN dbo.MenuOpciones AS m ON m.IdMenu = rm.IdMenu
        WHERE m.Estado = 1
          AND rm.IdRol IN ({0})
          AND rm.PuedeVer = 1
        ORDER BY rm.IdMenu;
        """;

    /// <summary>Menús con una acción concreta. <c>{0}</c>: marcadores de rol.</summary>
    private const string ConsultaMenusConAccion = """
        SELECT DISTINCT rm.IdMenu
        FROM dbo.RolMenu AS rm
        INNER JOIN dbo.MenuOpciones AS m ON m.IdMenu = rm.IdMenu
        WHERE m.Estado = 1
          AND rm.IdRol IN ({0})
          AND (CASE @Accion
                   WHEN 1 THEN rm.PuedeVer
                   WHEN 2 THEN rm.PuedeCrear
                   WHEN 3 THEN rm.PuedeEditar
                   ELSE rm.PuedeEliminar
               END) = 1
        ORDER BY rm.IdMenu;
        """;

    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Inicializa el repositorio con el proveedor de conexiones.
    /// </summary>
    /// <param name="connectionFactory">Proveedor de conexiones a la base de datos.</param>
    public PermisoRepository(SqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task<IReadOnlyList<RolAsignadoDto>> ObtenerRolesActivosAsync(
        int idUsuario,
        CancellationToken cancellationToken = default)
    {
        var roles = new List<RolAsignadoDto>();

        if (idUsuario <= 0)
        {
            return roles;
        }

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(ConsultaRolesActivos, conexion);
        comando.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = idUsuario;

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        var ordinalId = lector.GetOrdinal("IdRol");
        var ordinalNombre = lector.GetOrdinal("NombreRol");

        while (await lector.ReadAsync(cancellationToken))
        {
            roles.Add(new RolAsignadoDto
            {
                IdRol = lector.GetInt32(ordinalId),
                NombreRol = lector.IsDBNull(ordinalNombre) ? string.Empty : lector.GetString(ordinalNombre)
            });
        }

        return roles;
    }

    /// <inheritdoc />
    public async Task<bool> TienePermisoAsync(
        IReadOnlyList<int> idsRoles,
        string menuUrl,
        AccionPermiso accion,
        CancellationToken cancellationToken = default)
    {
        var roles = Normalizar(idsRoles);

        if (roles.Count == 0 || string.IsNullOrWhiteSpace(menuUrl))
        {
            return false;
        }

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(Construir(ConsultaTienePermiso, roles.Count), conexion);
        AgregarRoles(comando, roles);
        comando.Parameters.Add("@Url", SqlDbType.NVarChar, 400).Value = menuUrl;
        comando.Parameters.Add("@Accion", SqlDbType.Int).Value = (int)accion;

        var resultado = await comando.ExecuteScalarAsync(cancellationToken);

        return resultado is not null and not DBNull;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<int>> ObtenerMenusConPermisoAsync(
        IReadOnlyList<int> idsRoles,
        AccionPermiso accion,
        CancellationToken cancellationToken = default)
    {
        var roles = Normalizar(idsRoles);
        var menus = new List<int>();

        if (roles.Count == 0)
        {
            return menus;
        }

        var consulta = accion == AccionPermiso.Ver ? ConsultaMenusVisibles : ConsultaMenusConAccion;

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(Construir(consulta, roles.Count), conexion);
        AgregarRoles(comando, roles);

        if (accion != AccionPermiso.Ver)
        {
            comando.Parameters.Add("@Accion", SqlDbType.Int).Value = (int)accion;
        }

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            menus.Add(lector.GetInt32(0));
        }

        return menus;
    }

    private static string Construir(string plantilla, int cantidadRoles) =>
        string.Format(CultureInfo.InvariantCulture, plantilla, ListaMarcadores(cantidadRoles));

    private static string ListaMarcadores(int cantidad)
    {
        var marcadores = new string[cantidad];

        for (var indice = 0; indice < cantidad; indice++)
        {
            marcadores[indice] = $"@IdRol{indice}";
        }

        return string.Join(", ", marcadores);
    }

    private static void AgregarRoles(SqlCommand comando, IReadOnlyList<int> roles)
    {
        for (var indice = 0; indice < roles.Count; indice++)
        {
            comando.Parameters.Add($"@IdRol{indice}", SqlDbType.Int).Value = roles[indice];
        }
    }

    private static List<int> Normalizar(IReadOnlyList<int>? idsRoles) =>
        (idsRoles ?? [])
            .Where(idRol => idRol > 0)
            .Distinct()
            .ToList();
}
