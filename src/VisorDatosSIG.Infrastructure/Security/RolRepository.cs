using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.DTOs.Security;
using VisorDatosSIG.Application.Interfaces;
using VisorDatosSIG.Infrastructure.Data;

namespace VisorDatosSIG.Infrastructure.Security;

/// <summary>
/// Persistencia de roles (<c>dbo.Roles</c>), su matriz de permisos (<c>dbo.RolMenu</c>) y el
/// registro de auditoría de cada cambio en <c>dbo.Bitacora</c>.
/// </summary>
/// <remarks>
/// Reglas de la implementación:
/// <list type="bullet">
/// <item>Toda escritura confirmada va acompañada de su evento de bitácora en la <b>misma
/// transacción</b>: si falla la auditoría, el cambio se revierte.</item>
/// <item>Los roles nunca se borran: el estado lógico es <c>dbo.Roles.Estado</c>.</item>
/// <item>La matriz se reemplaza por completo: se eliminan las filas del rol que no vienen en la
/// petición y se insertan o actualizan las demás, de modo que repetir la petición deja el mismo
/// estado (idempotente).</item>
/// <item>El nombre del rol se compara sin distinción de mayúsculas ni acentos (<c>COLLATE
/// Latin1_General_CI_AI</c>) para que coincida con la normalización de <c>RolesSistema</c>.</item>
/// <item>Los parámetros de texto usan los tipos y longitudes físicos de la base
/// (<c>varchar(50)</c>, <c>varchar(200)</c>), evitando conversiones implícitas.</item>
/// </list>
/// </remarks>
public sealed class RolRepository : IRolRepository
{
    private const int LongitudNombre = 50;
    private const int LongitudDescripcion = 200;
    private const int LongitudModulo = 100;
    private const int LongitudAccion = 200;
    private const int LongitudEntidad = 200;
    private const int LongitudResultado = 60;
    private const int LongitudIp = 45;

    private const string ConsultaRoles = """
        SELECT r.IdRol,
               r.NombreRol,
               r.Descripcion,
               r.Estado,
               (SELECT COUNT(*)
                FROM dbo.UsuariosRoles AS ur
                INNER JOIN dbo.Usuarios AS u ON u.IdUsuario = ur.IdUsuario
                WHERE ur.IdRol = r.IdRol AND u.Activo = 1) AS UsuariosActivos
        FROM dbo.Roles AS r
        ORDER BY r.NombreRol;
        """;

    private const string ConsultaRolPorId = """
        SELECT r.IdRol,
               r.NombreRol,
               r.Descripcion,
               r.Estado,
               (SELECT COUNT(*)
                FROM dbo.UsuariosRoles AS ur
                INNER JOIN dbo.Usuarios AS u ON u.IdUsuario = ur.IdUsuario
                WHERE ur.IdRol = r.IdRol AND u.Activo = 1) AS UsuariosActivos
        FROM dbo.Roles AS r
        WHERE r.IdRol = @IdRol;
        """;

    private const string ConsultaExisteNombre = """
        SELECT TOP (1) 1
        FROM dbo.Roles AS r
        WHERE r.NombreRol COLLATE Latin1_General_CI_AI = @NombreRol COLLATE Latin1_General_CI_AI
          AND (@IdRolExcluido IS NULL OR r.IdRol <> @IdRolExcluido);
        """;

    private const string InsertarRol = """
        INSERT INTO dbo.Roles (NombreRol, Descripcion, Estado)
        OUTPUT INSERTED.IdRol
        VALUES (@NombreRol, @Descripcion, 1);
        """;

    private const string ActualizarRol = """
        UPDATE dbo.Roles
        SET NombreRol = @NombreRol,
            Descripcion = @Descripcion
        WHERE IdRol = @IdRol;
        """;

    private const string ActualizarEstadoRol = """
        UPDATE dbo.Roles
        SET Estado = @Estado
        WHERE IdRol = @IdRol;
        """;

    private const string ConsultaPermisosRol = """
        SELECT rm.IdMenu,
               rm.PuedeVer,
               rm.PuedeCrear,
               rm.PuedeEditar,
               rm.PuedeEliminar
        FROM dbo.RolMenu AS rm
        WHERE rm.IdRol = @IdRol
          AND (rm.PuedeVer = 1 OR rm.PuedeCrear = 1 OR rm.PuedeEditar = 1 OR rm.PuedeEliminar = 1)
        ORDER BY rm.IdMenu;
        """;

    /// <summary>Cantidad de menús de la lista que están activos. <c>{0}</c>: marcadores.</summary>
    private const string ContarMenusActivos = """
        SELECT COUNT(*)
        FROM dbo.MenuOpciones AS m
        WHERE m.Estado = 1
          AND m.IdMenu IN ({0});
        """;

    /// <summary>Comprueba la existencia del rol tomando un bloqueo dentro de la transacción.</summary>
    private const string BloquearRol = """
        SELECT r.IdRol
        FROM dbo.Roles AS r WITH (UPDLOCK, ROWLOCK)
        WHERE r.IdRol = @IdRol;
        """;

    /// <summary>Elimina las filas del rol que no vienen en la petición. <c>{0}</c>: marcadores.</summary>
    private const string EliminarPermisosFueraDe = """
        DELETE FROM dbo.RolMenu
        WHERE IdRol = @IdRol
          AND IdMenu NOT IN ({0});
        """;

    private const string EliminarTodosLosPermisos = "DELETE FROM dbo.RolMenu WHERE IdRol = @IdRol;";

    private const string GuardarPermiso = """
        UPDATE dbo.RolMenu
        SET PuedeVer = @PuedeVer,
            PuedeCrear = @PuedeCrear,
            PuedeEditar = @PuedeEditar,
            PuedeEliminar = @PuedeEliminar
        WHERE IdRol = @IdRol AND IdMenu = @IdMenu;

        IF @@ROWCOUNT = 0
        BEGIN
            INSERT INTO dbo.RolMenu (IdRol, IdMenu, PuedeVer, PuedeCrear, PuedeEditar, PuedeEliminar)
            VALUES (@IdRol, @IdMenu, @PuedeVer, @PuedeCrear, @PuedeEditar, @PuedeEliminar);
        END
        """;

    private const string InsertarAuditoria = """
        INSERT INTO dbo.Bitacora (IdUsuario, FechaHora, Modulo, Accion, Entidad, IdEntidad, Resultado, Detalle, IP)
        VALUES (@IdUsuario, SYSDATETIME(), @Modulo, @Accion, @Entidad, @IdEntidad, @Resultado, @Detalle, @IP);
        """;

    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Inicializa el repositorio con el proveedor de conexiones.
    /// </summary>
    /// <param name="connectionFactory">Proveedor de conexiones a la base de datos.</param>
    public RolRepository(SqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task<IReadOnlyList<RolFilaDto>> ObtenerRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = new List<RolFilaDto>();

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(ConsultaRoles, conexion);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        while (await lector.ReadAsync(cancellationToken))
        {
            roles.Add(Mapear(lector));
        }

        return roles;
    }

    /// <inheritdoc />
    public async Task<RolFilaDto?> ObtenerRolPorIdAsync(int idRol, CancellationToken cancellationToken = default)
    {
        if (idRol <= 0)
        {
            return null;
        }

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(ConsultaRolPorId, conexion);
        comando.Parameters.Add("@IdRol", SqlDbType.Int).Value = idRol;

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        return await lector.ReadAsync(cancellationToken) ? Mapear(lector) : null;
    }

    /// <inheritdoc />
    public async Task<bool> ExisteNombreRolAsync(
        string nombreRol,
        int? idRolExcluido,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(nombreRol))
        {
            return false;
        }

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(ConsultaExisteNombre, conexion);
        comando.Parameters.Add("@NombreRol", SqlDbType.VarChar, LongitudNombre).Value = Recortar(nombreRol, LongitudNombre);
        comando.Parameters.Add("@IdRolExcluido", SqlDbType.Int).Value = idRolExcluido.HasValue
            ? idRolExcluido.Value
            : DBNull.Value;

        var resultado = await comando.ExecuteScalarAsync(cancellationToken);

        return resultado is not null and not DBNull;
    }

    private static RolFilaDto Mapear(SqlDataReader lector)
    {
        var ordinalNombre = lector.GetOrdinal("NombreRol");
        var ordinalDescripcion = lector.GetOrdinal("Descripcion");
        var ordinalEstado = lector.GetOrdinal("Estado");

        return new RolFilaDto
        {
            IdRol = lector.GetInt32(lector.GetOrdinal("IdRol")),
            NombreRol = lector.IsDBNull(ordinalNombre) ? string.Empty : lector.GetString(ordinalNombre),
            Descripcion = lector.IsDBNull(ordinalDescripcion) ? null : lector.GetString(ordinalDescripcion),
            // Estado es bit en dbo.Roles; se normaliza a bool sin acoplar el DTO al tipo físico.
            Activo = !lector.IsDBNull(ordinalEstado)
                && Convert.ToBoolean(lector.GetValue(ordinalEstado), CultureInfo.InvariantCulture),
            CantidadUsuariosActivos = Convert.ToInt32(
                lector.GetValue(lector.GetOrdinal("UsuariosActivos")),
                CultureInfo.InvariantCulture)
        };
    }

    /// <inheritdoc />
    public async Task<int> CrearRolAsync(
        string nombreRol,
        string? descripcion,
        Func<int, BitacoraRegistroDto> crearAuditoria,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreRol);
        ArgumentNullException.ThrowIfNull(crearAuditoria);

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        int idRol;

        await using (var comando = new SqlCommand(InsertarRol, conexion, transaccion))
        {
            AgregarNombreYDescripcion(comando, nombreRol, descripcion);
            idRol = Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        }

        // La auditoría recibe el identificador generado: por eso se registra después del INSERT.
        await RegistrarAuditoriaAsync(conexion, transaccion, crearAuditoria(idRol), cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        return idRol;
    }

    /// <inheritdoc />
    public async Task<bool> ActualizarRolAsync(
        int idRol,
        string nombreRol,
        string? descripcion,
        BitacoraRegistroDto auditoria,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreRol);
        ArgumentNullException.ThrowIfNull(auditoria);

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        int filasAfectadas;

        await using (var comando = new SqlCommand(ActualizarRol, conexion, transaccion))
        {
            comando.Parameters.Add("@IdRol", SqlDbType.Int).Value = idRol;
            AgregarNombreYDescripcion(comando, nombreRol, descripcion);
            filasAfectadas = await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        if (filasAfectadas == 0)
        {
            await transaccion.RollbackAsync(cancellationToken);
            return false;
        }

        await RegistrarAuditoriaAsync(conexion, transaccion, auditoria, cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> ActualizarEstadoRolAsync(
        int idRol,
        bool activo,
        BitacoraRegistroDto auditoria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditoria);

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        int filasAfectadas;

        await using (var comando = new SqlCommand(ActualizarEstadoRol, conexion, transaccion))
        {
            comando.Parameters.Add("@IdRol", SqlDbType.Int).Value = idRol;
            comando.Parameters.Add("@Estado", SqlDbType.Bit).Value = activo;
            filasAfectadas = await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        if (filasAfectadas == 0)
        {
            await transaccion.RollbackAsync(cancellationToken);
            return false;
        }

        await RegistrarAuditoriaAsync(conexion, transaccion, auditoria, cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        return true;
    }

    private static void AgregarNombreYDescripcion(SqlCommand comando, string nombreRol, string? descripcion)
    {
        comando.Parameters.Add("@NombreRol", SqlDbType.VarChar, LongitudNombre).Value =
            Recortar(nombreRol.Trim(), LongitudNombre);

        var descripcionNormalizada = string.IsNullOrWhiteSpace(descripcion)
            ? null
            : Recortar(descripcion.Trim(), LongitudDescripcion);

        comando.Parameters.Add("@Descripcion", SqlDbType.VarChar, LongitudDescripcion).Value =
            (object?)descripcionNormalizada ?? DBNull.Value;
    }

    private static async Task RegistrarAuditoriaAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        BitacoraRegistroDto auditoria,
        CancellationToken cancellationToken)
    {
        await using var comando = new SqlCommand(InsertarAuditoria, conexion, transaccion);

        comando.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = auditoria.IdUsuario.HasValue
            ? auditoria.IdUsuario.Value
            : DBNull.Value;
        comando.Parameters.Add("@Modulo", SqlDbType.NVarChar, LongitudModulo).Value = Recortar(auditoria.Modulo, LongitudModulo);
        comando.Parameters.Add("@Accion", SqlDbType.NVarChar, LongitudAccion).Value = Recortar(auditoria.Accion, LongitudAccion);
        comando.Parameters.Add("@Entidad", SqlDbType.NVarChar, LongitudEntidad).Value =
            string.IsNullOrWhiteSpace(auditoria.Entidad) ? DBNull.Value : Recortar(auditoria.Entidad, LongitudEntidad);
        comando.Parameters.Add("@IdEntidad", SqlDbType.BigInt).Value = auditoria.IdEntidad.HasValue
            ? auditoria.IdEntidad.Value
            : DBNull.Value;
        comando.Parameters.Add("@Resultado", SqlDbType.NVarChar, LongitudResultado).Value = Recortar(auditoria.Resultado, LongitudResultado);
        comando.Parameters.Add("@Detalle", SqlDbType.NVarChar, -1).Value =
            string.IsNullOrWhiteSpace(auditoria.Detalle) ? DBNull.Value : auditoria.Detalle;
        comando.Parameters.Add("@IP", SqlDbType.VarChar, LongitudIp).Value =
            string.IsNullOrWhiteSpace(auditoria.Ip) ? DBNull.Value : Recortar(auditoria.Ip, LongitudIp);

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PermisoMenuFilaDto>> ObtenerPermisosRolAsync(
        int idRol,
        CancellationToken cancellationToken = default)
    {
        var permisos = new List<PermisoMenuFilaDto>();

        if (idRol <= 0)
        {
            return permisos;
        }

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(ConsultaPermisosRol, conexion);
        comando.Parameters.Add("@IdRol", SqlDbType.Int).Value = idRol;

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

        var ordinalMenu = lector.GetOrdinal("IdMenu");
        var ordinalVer = lector.GetOrdinal("PuedeVer");
        var ordinalCrear = lector.GetOrdinal("PuedeCrear");
        var ordinalEditar = lector.GetOrdinal("PuedeEditar");
        var ordinalEliminar = lector.GetOrdinal("PuedeEliminar");

        while (await lector.ReadAsync(cancellationToken))
        {
            permisos.Add(new PermisoMenuFilaDto
            {
                IdMenu = lector.GetInt32(ordinalMenu),
                PuedeVer = EsVerdadero(lector, ordinalVer),
                PuedeCrear = EsVerdadero(lector, ordinalCrear),
                PuedeEditar = EsVerdadero(lector, ordinalEditar),
                PuedeEliminar = EsVerdadero(lector, ordinalEliminar)
            });
        }

        return permisos;
    }

    /// <inheritdoc />
    public async Task<bool> ExistenMenusActivosAsync(
        IReadOnlyList<int> idsMenu,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(idsMenu);

        var idsDistintos = idsMenu.Where(id => id > 0).Distinct().ToArray();

        if (idsDistintos.Length == 0)
        {
            return true;
        }

        var sql = string.Format(
            CultureInfo.InvariantCulture,
            ContarMenusActivos,
            ListaMarcadores(idsDistintos.Length));

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(sql, conexion);
        AgregarIdsMenu(comando, idsDistintos);

        var encontrados = Convert.ToInt32(await comando.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

        return encontrados == idsDistintos.Length;
    }

    /// <inheritdoc />
    public async Task<bool> GuardarPermisosRolAsync(
        int idRol,
        IReadOnlyList<PermisoMenuFilaDto> permisos,
        BitacoraRegistroDto auditoria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permisos);
        ArgumentNullException.ThrowIfNull(auditoria);

        // El reemplazo es completo: las filas sin ningún permiso otorgado se eliminan de la matriz.
        var filas = permisos
            .Where(permiso => permiso.TieneAlgunPermiso && permiso.IdMenu > 0)
            .GroupBy(permiso => permiso.IdMenu)
            .Select(grupo => grupo.Last())
            .OrderBy(fila => fila.IdMenu)
            .ToArray();

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var transaccion = (SqlTransaction)await conexion.BeginTransactionAsync(cancellationToken);

        if (!await ExisteRolAsync(conexion, transaccion, idRol, cancellationToken))
        {
            await transaccion.RollbackAsync(cancellationToken);
            return false;
        }

        await EliminarPermisosAsync(conexion, transaccion, idRol, filas, cancellationToken);

        foreach (var fila in filas)
        {
            await GuardarPermisoAsync(conexion, transaccion, idRol, fila, cancellationToken);
        }

        await RegistrarAuditoriaAsync(conexion, transaccion, auditoria, cancellationToken);
        await transaccion.CommitAsync(cancellationToken);

        return true;
    }

    private static async Task<bool> ExisteRolAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        int idRol,
        CancellationToken cancellationToken)
    {
        await using var comando = new SqlCommand(BloquearRol, conexion, transaccion);
        comando.Parameters.Add("@IdRol", SqlDbType.Int).Value = idRol;

        var resultado = await comando.ExecuteScalarAsync(cancellationToken);

        return resultado is not null and not DBNull;
    }

    private static async Task EliminarPermisosAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        int idRol,
        IReadOnlyList<PermisoMenuFilaDto> filas,
        CancellationToken cancellationToken)
    {
        var sql = filas.Count == 0
            ? EliminarTodosLosPermisos
            : string.Format(CultureInfo.InvariantCulture, EliminarPermisosFueraDe, ListaMarcadores(filas.Count));

        await using var comando = new SqlCommand(sql, conexion, transaccion);
        comando.Parameters.Add("@IdRol", SqlDbType.Int).Value = idRol;
        AgregarIdsMenu(comando, filas.Select(fila => fila.IdMenu));

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task GuardarPermisoAsync(
        SqlConnection conexion,
        SqlTransaction transaccion,
        int idRol,
        PermisoMenuFilaDto fila,
        CancellationToken cancellationToken)
    {
        await using var comando = new SqlCommand(GuardarPermiso, conexion, transaccion);

        comando.Parameters.Add("@IdRol", SqlDbType.Int).Value = idRol;
        comando.Parameters.Add("@IdMenu", SqlDbType.Int).Value = fila.IdMenu;
        comando.Parameters.Add("@PuedeVer", SqlDbType.Bit).Value = fila.PuedeVer;
        comando.Parameters.Add("@PuedeCrear", SqlDbType.Bit).Value = fila.PuedeCrear;
        comando.Parameters.Add("@PuedeEditar", SqlDbType.Bit).Value = fila.PuedeEditar;
        comando.Parameters.Add("@PuedeEliminar", SqlDbType.Bit).Value = fila.PuedeEliminar;

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Recortar(string valor, int longitudMaxima) =>
        valor.Length <= longitudMaxima ? valor : valor[..longitudMaxima];

    private static bool EsVerdadero(SqlDataReader lector, int ordinal) =>
        !lector.IsDBNull(ordinal) && Convert.ToBoolean(lector.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static string ListaMarcadores(int cantidad) => string.Join(
        ", ",
        Enumerable.Range(0, cantidad).Select(indice => "@IdMenu" + indice.ToString(CultureInfo.InvariantCulture)));

    private static void AgregarIdsMenu(SqlCommand comando, IEnumerable<int> idsMenu)
    {
        var indice = 0;

        foreach (var idMenu in idsMenu)
        {
            comando.Parameters.Add("@IdMenu" + indice.ToString(CultureInfo.InvariantCulture), SqlDbType.Int).Value = idMenu;
            indice++;
        }
    }
}
