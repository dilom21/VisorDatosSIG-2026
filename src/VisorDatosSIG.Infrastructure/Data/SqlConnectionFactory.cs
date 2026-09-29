using Microsoft.Data.SqlClient;

namespace VisorDatosSIG.Infrastructure.Data;

/// <summary>
/// Proveedor de conexiones a la base de datos VisorDatosSIG.
/// </summary>
/// <remarks>
/// La cadena de conexión llega desde la configuración de la aplicación
/// (User Secrets, variable de entorno o appsettings no versionado) y nunca se escribe
/// en el código fuente ni se registra en logs.
/// </remarks>
public sealed class SqlConnectionFactory
{
    /// <summary>Nombre de la cadena de conexión en la configuración.</summary>
    public const string ConnectionStringName = "VisorDatosSIG";

    private readonly string _connectionString;

    /// <summary>
    /// Inicializa el proveedor con la cadena de conexión configurada.
    /// </summary>
    /// <param name="connectionString">Cadena de conexión a SQL Server.</param>
    public SqlConnectionFactory(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{ConnectionStringName}'. " +
                "Configúrela con User Secrets (dotnet user-secrets), una variable de entorno " +
                "(ConnectionStrings__" + ConnectionStringName + ") o un archivo appsettings no versionado.");
        }

        _connectionString = connectionString;
    }

    /// <summary>
    /// Abre una conexión a la base de datos.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    public async Task<SqlConnection> AbrirAsync(CancellationToken cancellationToken = default)
    {
        var conexion = new SqlConnection(_connectionString);
        await conexion.OpenAsync(cancellationToken);
        return conexion;
    }
}
