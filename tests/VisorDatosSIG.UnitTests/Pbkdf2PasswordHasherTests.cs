using System.Security.Cryptography;
using System.Text;
using VisorDatosSIG.Infrastructure.Authentication;

namespace VisorDatosSIG.UnitTests;

/// <summary>
/// Pruebas del esquema de contraseñas PBKDF2-SHA256.
/// </summary>
public sealed class Pbkdf2PasswordHasherTests
{
    private const int IteracionesSistema = 100000;

    /// <summary>
    /// Salt y hash de siembra del script 05_Seguridad_Login.sql (contraseña de prueba Admin123!).
    /// Verifica que la implementación sea compatible con los registros almacenados.
    /// </summary>
    private static readonly byte[] SaltSiembra =
        Convert.FromHexString("A65A322F14A1CB879EA3D63822D9E8A73176A8DF24CE6376268451B1F12BA771");

    private static readonly byte[] HashSiembra =
        Convert.FromHexString("D26853A2FC032A9D1081113118D545852915DA9F70E2E30BC38BD186473BC39F");

    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact(DisplayName = "PBKDF2: la contraseña correcta de siembra es aceptada (compatible con la base)")]
    public void ContrasenaDeSiembraEsAceptada()
    {
        var resultado = _hasher.Verificar("Admin123!", SaltSiembra, IteracionesSistema, HashSiembra);

        Assert.True(resultado);
    }

    [Fact(DisplayName = "PBKDF2: una contraseña incorrecta es rechazada")]
    public void ContrasenaIncorrectaEsRechazada()
    {
        Assert.False(_hasher.Verificar("Admin123", SaltSiembra, IteracionesSistema, HashSiembra));
        Assert.False(_hasher.Verificar("admin123!", SaltSiembra, IteracionesSistema, HashSiembra));
        Assert.False(_hasher.Verificar("OtraClave!", SaltSiembra, IteracionesSistema, HashSiembra));
        Assert.False(_hasher.Verificar(string.Empty, SaltSiembra, IteracionesSistema, HashSiembra));
    }

    [Fact(DisplayName = "PBKDF2: el hash calculado coincide con el derivado por .NET (32 bytes, SHA-256)")]
    public void HashCalculadoCoincideConElDerivado()
    {
        var salt = RandomNumberGenerator.GetBytes(32);

        var esperado = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes("ClaveDePrueba#2026"),
            salt,
            IteracionesSistema,
            HashAlgorithmName.SHA256,
            32);

        Assert.Equal(32, esperado.Length);
        Assert.True(_hasher.Verificar("ClaveDePrueba#2026", salt, IteracionesSistema, esperado));
        Assert.False(_hasher.Verificar("ClaveDePrueba#2027", salt, IteracionesSistema, esperado));
    }

    [Fact(DisplayName = "PBKDF2: una salt distinta produce un hash distinto para la misma contraseña")]
    public void SaltDistintaProduceHashDistinto()
    {
        var saltA = RandomNumberGenerator.GetBytes(32);
        var saltB = RandomNumberGenerator.GetBytes(32);

        var hashA = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes("Admin123!"), saltA, IteracionesSistema, HashAlgorithmName.SHA256, 32);

        Assert.True(_hasher.Verificar("Admin123!", saltA, IteracionesSistema, hashA));
        Assert.False(_hasher.Verificar("Admin123!", saltB, IteracionesSistema, hashA));
    }

    [Fact(DisplayName = "PBKDF2: iteraciones distintas a las almacenadas son rechazadas")]
    public void IteracionesDistintasSonRechazadas()
    {
        Assert.False(_hasher.Verificar("Admin123!", SaltSiembra, IteracionesSistema - 1, HashSiembra));
        Assert.False(_hasher.Verificar("Admin123!", SaltSiembra, 1, HashSiembra));
    }

    [Theory(DisplayName = "PBKDF2: datos incompletos son rechazados sin excepción")]
    [InlineData(0)]
    [InlineData(1)]
    public void DatosInvalidosSonRechazados(int caso)
    {
        var resultado = caso switch
        {
            0 => _hasher.Verificar("Admin123!", [], IteracionesSistema, HashSiembra),
            _ => _hasher.Verificar("Admin123!", SaltSiembra, IteracionesSistema, [])
        };

        Assert.False(resultado);
    }
}
