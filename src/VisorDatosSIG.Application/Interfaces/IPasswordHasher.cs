namespace VisorDatosSIG.Application.Interfaces;

/// <summary>
/// Contrato de verificación de contraseñas.
/// </summary>
/// <remarks>
/// El esquema del proyecto es PBKDF2-SHA256 con salt aleatoria e iteraciones configurables
/// (el valor del sistema es 100000 y el hash de 32 bytes). No se utilizan Argon2 ni BCrypt.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>
    /// Verifica una contraseña contra el hash almacenado, comparando en tiempo constante.
    /// </summary>
    /// <param name="password">Contraseña recibida (en memoria, nunca se registra).</param>
    /// <param name="salt">Salt almacenada para el usuario.</param>
    /// <param name="iteraciones">Iteraciones configuradas para el usuario.</param>
    /// <param name="hashEsperado">Hash PBKDF2 almacenado para el usuario.</param>
    /// <returns><c>true</c> cuando la contraseña es correcta.</returns>
    bool Verificar(string password, byte[] salt, int iteraciones, byte[] hashEsperado);
}
