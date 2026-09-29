using System.Security.Cryptography;
using System.Text;
using VisorDatosSIG.Application.Interfaces;

namespace VisorDatosSIG.Infrastructure.Authentication;

/// <summary>
/// Verificación de contraseñas con PBKDF2-SHA256, compatible con el esquema almacenado
/// en dbo.Usuarios (salt aleatoria, iteraciones configurables, hash de 32 bytes).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>No se utilizan Argon2 ni BCrypt.</item>
/// <item>La contraseña recibida se usa únicamente en memoria: nunca se registra ni se persiste.</item>
/// <item>La comparación se realiza en tiempo constante con
/// <see cref="CryptographicOperations.FixedTimeEquals(ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>.</item>
/// </list>
/// </remarks>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    /// <inheritdoc />
    public bool Verificar(string password, byte[] salt, int iteraciones, byte[] hashEsperado)
    {
        if (string.IsNullOrEmpty(password)
            || salt is null || salt.Length == 0
            || hashEsperado is null || hashEsperado.Length == 0
            || iteraciones <= 0)
        {
            return false;
        }

        // Se derivan tantos bytes como tenga el hash almacenado (32 en este proyecto),
        // de modo que la verificación sea compatible con los registros existentes.
        var calculado = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iteraciones,
            HashAlgorithmName.SHA256,
            hashEsperado.Length);

        return CryptographicOperations.FixedTimeEquals(calculado, hashEsperado);
    }
}
