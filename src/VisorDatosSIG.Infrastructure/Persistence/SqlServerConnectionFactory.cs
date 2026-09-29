using Microsoft.Data.SqlClient;

namespace VisorDatosSIG.Infrastructure.Persistence;

public sealed class SqlServerConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("La cadena de conexión no puede estar vacía.", nameof(connectionString));
        }

        _connectionString = connectionString;
    }

    public SqlConnection Create() => new(_connectionString);
}
