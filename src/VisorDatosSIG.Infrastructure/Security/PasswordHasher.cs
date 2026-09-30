using System.Security.Cryptography;

namespace VisorDatosSIG.Infrastructure.Security;

/// <summary>
/// Utilidad criptográfica para contraseñas con hash PBKDF2 y sal (RF-SEG-01).
/// Compatible con el esquema de dbo.Usuarios en SQL Server.
/// </summary>
public static class PasswordHasher
{
    private const int DefaultIterations = 100000;
    private const int HashSize = 32;
    private const int SaltSize = 32;

    public static (byte[] Hash, byte[] Salt, int Iterations) HashPassword(string password, int iterations = DefaultIterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return (hash, salt, iterations);
    }

    public static bool VerifyPassword(string password, byte[] salt, byte[] expectedHash, int iterations = DefaultIterations)
    {
        if (string.IsNullOrEmpty(password) || salt is null || expectedHash is null)
        {
            return false;
        }

        var actualIterations = iterations > 0 ? iterations : DefaultIterations;
        var computedHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            actualIterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }
}
