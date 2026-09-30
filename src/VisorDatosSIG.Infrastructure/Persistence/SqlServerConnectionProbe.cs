using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.DTOs;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Persistence;

public sealed class SqlServerConnectionProbe : ISqlServerConnectionProbe
{
    private static readonly string[] RequiredTables =
    [
        "Manzanas",
        "Lotes",
        "CodigosFijos",
        "Vias"
    ];

    private readonly SqlServerConnectionFactory _connectionFactory;

    public SqlServerConnectionProbe(SqlServerConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = _connectionFactory.Create();
            await connection.OpenAsync(cancellationToken);

            var missingTables = await FindMissingTablesAsync(connection, cancellationToken);
            var hasWriteAccess = missingTables.Count == 0
                && await HasWriteAccessAsync(connection, cancellationToken);

            var message = missingTables.Count > 0
                ? "La conexión es correcta, pero faltan tablas requeridas."
                : !hasWriteAccess
                    ? "La conexión es correcta, pero faltan permisos de escritura."
                    : "Conexión, base de datos, tablas y permisos verificados.";

            return new ConnectionTestResult
            {
                CanConnect = true,
                DatabaseExists = true,
                RequiredTablesExist = missingTables.Count == 0,
                HasWriteAccess = hasWriteAccess,
                MissingTables = missingTables,
                Message = message
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException exception)
        {
            return new ConnectionTestResult
            {
                CanConnect = false,
                DatabaseExists = false,
                RequiredTablesExist = false,
                HasWriteAccess = false,
                Message = "No se pudo abrir la conexión con SQL Server.",
                Errors = [$"SQL {exception.Number}: {exception.Message}"]
            };
        }
        catch (Exception exception)
        {
            return new ConnectionTestResult
            {
                CanConnect = false,
                DatabaseExists = false,
                RequiredTablesExist = false,
                HasWriteAccess = false,
                Message = "No se pudo comprobar la configuración de SQL Server.",
                Errors = [$"{exception.GetType().Name}: {exception.Message}"]
            };
        }
    }

    private static async Task<IReadOnlyList<string>> FindMissingTablesAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TABLE_NAME
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = 'dbo'
              AND TABLE_TYPE = 'BASE TABLE';
            """;

        var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            existingTables.Add(reader.GetString(0));
        }

        return RequiredTables
            .Where(table => !existingTables.Contains(table))
            .ToArray();
    }

    private static async Task<bool> HasWriteAccessAsync(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT CONVERT(bit,
                CASE WHEN
                    HAS_PERMS_BY_NAME('dbo.Manzanas', 'OBJECT', 'INSERT') = 1 AND
                    HAS_PERMS_BY_NAME('dbo.Manzanas', 'OBJECT', 'DELETE') = 1 AND
                    HAS_PERMS_BY_NAME('dbo.Lotes', 'OBJECT', 'INSERT') = 1 AND
                    HAS_PERMS_BY_NAME('dbo.Lotes', 'OBJECT', 'DELETE') = 1 AND
                    HAS_PERMS_BY_NAME('dbo.CodigosFijos', 'OBJECT', 'INSERT') = 1 AND
                    HAS_PERMS_BY_NAME('dbo.CodigosFijos', 'OBJECT', 'DELETE') = 1 AND
                    HAS_PERMS_BY_NAME('dbo.Vias', 'OBJECT', 'INSERT') = 1 AND
                    HAS_PERMS_BY_NAME('dbo.Vias', 'OBJECT', 'DELETE') = 1
                THEN 1 ELSE 0 END);
            """;

        await using var command = new SqlCommand(sql, connection);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is bool hasAccess && hasAccess;
    }
}
