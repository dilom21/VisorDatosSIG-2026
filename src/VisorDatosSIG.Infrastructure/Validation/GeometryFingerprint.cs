using NetTopologySuite.Geometries;

namespace VisorDatosSIG.Infrastructure.Validation;

/// <summary>
/// Huella (fingerprint) de 128 bits de una geometría, utilizada para preagrupar
/// candidatos a duplicados sin conservar las geometrías en memoria.
/// </summary>
/// <remarks>
/// <para>
/// Se calcula sobre la representación binaria (WKB) de la geometría aplicando dos
/// reducciones FNV-1a independientes: recorrido directo y recorrido inverso.
/// </para>
/// <para>
/// La huella se usa solo para agrupar candidatos. Cuando la duplicación implica omitir
/// un registro, la igualdad se confirma además con
/// <see cref="Geometry.EqualsExact(Geometry)"/>.
/// </para>
/// <para>
/// Limitación documentada: si el lector de shapefiles no expone las ordenadas Z/M,
/// esas ordenadas no forman parte de la huella ni de la comparación.
/// </para>
/// </remarks>
internal static class GeometryFingerprint
{
    private const ulong FnvOffsetBasis = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>
    /// Calcula la huella de la geometría en formato hexadecimal (32 caracteres).
    /// </summary>
    public static string Compute(Geometry geometry)
    {
        var wkb = geometry.ToBinary();

        return string.Concat(
            HashForward(wkb).ToString("x16"),
            HashBackward(wkb).ToString("x16"));
    }

    private static ulong HashForward(byte[] data)
    {
        var hash = FnvOffsetBasis;
        foreach (var octet in data)
        {
            hash ^= octet;
            hash *= FnvPrime;
        }

        return hash;
    }

    private static ulong HashBackward(byte[] data)
    {
        var hash = FnvOffsetBasis;
        for (var index = data.Length - 1; index >= 0; index--)
        {
            hash ^= data[index];
            hash *= FnvPrime;
        }

        return hash;
    }
}
