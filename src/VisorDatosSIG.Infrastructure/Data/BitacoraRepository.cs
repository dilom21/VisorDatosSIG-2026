using System.Data;
using Microsoft.Data.SqlClient;
using VisorDatosSIG.Application.DTOs.Authentication;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Data;

/// <summary>
/// Registro de eventos en dbo.Bitacora mediante consultas parametrizadas.
/// </summary>
/// <remarks>
/// Nunca se guardan contraseñas, hashes, salts ni tokens: solo datos de auditoría.
/// </remarks>
public sealed class BitacoraRepository : IBitacoraRepository
{
    private const int LongitudModulo = 100;
    private const int LongitudAccion = 200;
    private const int LongitudEntidad = 200;
    private const int LongitudResultado = 60;
    private const int LongitudIp = 45;

    private const string InsertarEvento = """
        INSERT INTO dbo.Bitacora (IdUsuario, FechaHora, Modulo, Accion, Entidad, IdEntidad, Resultado, Detalle, IP)
        VALUES (@IdUsuario, SYSDATETIME(), @Modulo, @Accion, @Entidad, @IdEntidad, @Resultado, @Detalle, @IP);
        """;

    private readonly SqlConnectionFactory _connectionFactory;

    /// <summary>
    /// Inicializa el repositorio con el proveedor de conexiones.
    /// </summary>
    public BitacoraRepository(SqlConnectionFactory connectionFactory) =>
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    /// <inheritdoc />
    public async Task RegistrarAsync(BitacoraRegistroDto registro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);

        await using var conexion = await _connectionFactory.AbrirAsync(cancellationToken);
        await using var comando = new SqlCommand(InsertarEvento, conexion);

        comando.Parameters.Add("@IdUsuario", SqlDbType.Int).Value = registro.IdUsuario.HasValue
            ? registro.IdUsuario.Value
            : DBNull.Value;
        comando.Parameters.Add("@Modulo", SqlDbType.NVarChar, LongitudModulo).Value = Recortar(registro.Modulo, LongitudModulo);
        comando.Parameters.Add("@Accion", SqlDbType.NVarChar, LongitudAccion).Value = Recortar(registro.Accion, LongitudAccion);
        comando.Parameters.Add("@Entidad", SqlDbType.NVarChar, LongitudEntidad).Value =
            string.IsNullOrWhiteSpace(registro.Entidad) ? DBNull.Value : Recortar(registro.Entidad, LongitudEntidad);
        comando.Parameters.Add("@IdEntidad", SqlDbType.BigInt).Value = registro.IdEntidad.HasValue
            ? registro.IdEntidad.Value
            : DBNull.Value;
        comando.Parameters.Add("@Resultado", SqlDbType.NVarChar, LongitudResultado).Value = Recortar(registro.Resultado, LongitudResultado);
        comando.Parameters.Add("@Detalle", SqlDbType.NVarChar, -1).Value =
            string.IsNullOrWhiteSpace(registro.Detalle) ? DBNull.Value : registro.Detalle;
        comando.Parameters.Add("@IP", SqlDbType.VarChar, LongitudIp).Value =
            string.IsNullOrWhiteSpace(registro.Ip) ? DBNull.Value : Recortar(registro.Ip, LongitudIp);

        await comando.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Recortar(string valor, int longitudMaxima) =>
        valor.Length <= longitudMaxima ? valor : valor[..longitudMaxima];
}
