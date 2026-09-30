using System.Text;
using NetTopologySuite.Geometries;

namespace VisorDatosSIG.Infrastructure.Validation;

/// <summary>
/// Detecta duplicados exactos de registros dentro de una capa.
/// </summary>
/// <remarks>
/// <para>
/// El algoritmo es de una sola pasada y O(n): se construye una clave con la huella
/// geométrica (y los campos de atributos configurados) y se guarda la primera aparición.
/// </para>
/// <para>
/// Cuando la detección implica omitir registros, se conserva la geometría de la primera
/// aparición para confirmar la igualdad con <see cref="Geometry.EqualsExact(Geometry)"/>.
/// </para>
/// </remarks>
internal sealed class ExactDuplicateDetector
{
    private sealed class Occurrence
    {
        public required long RecordNumber { get; init; }

        public required Geometry? Geometry { get; init; }

        public int Count { get; set; }
    }

    private readonly DuplicateRule _rule;
    private readonly Dictionary<string, Occurrence> _occurrences = new(StringComparer.Ordinal);
    private readonly List<long> _redundantRecordNumbers = new();

    private int _fingerprintCollisions;

    /// <summary>
    /// Inicializa el detector con la regla de duplicados de la capa.
    /// </summary>
    public ExactDuplicateDetector(DuplicateRule rule) => _rule = rule;

    /// <summary>Cantidad de registros redundantes (los que no son la primera aparición).</summary>
    public long RedundantCount => _redundantRecordNumbers.Count;

    /// <summary>Cantidad de grupos con más de un registro idéntico.</summary>
    public long GroupCount { get; private set; }

    /// <summary>Posiciones de lectura de los registros redundantes.</summary>
    public IReadOnlyList<long> RedundantRecordNumbers => _redundantRecordNumbers;

    /// <summary>Cantidad de colisiones de huella que no se confirmaron como duplicados.</summary>
    public int FingerprintCollisions => _fingerprintCollisions;

    /// <summary>
    /// Analiza un registro.
    /// </summary>
    /// <param name="recordNumber">Posición de lectura del registro.</param>
    /// <param name="geometry">Geometría del registro.</param>
    /// <param name="values">Valores de los atributos del registro.</param>
    /// <returns>
    /// 0 cuando el registro no es duplicado; en caso contrario, la posición de lectura
    /// del registro conservado (la primera aparición del duplicado exacto).
    /// </returns>
    public long Inspect(long recordNumber, Geometry geometry, IReadOnlyDictionary<string, string?> values)
    {
        var key = BuildKey(geometry, values);

        if (!_occurrences.TryGetValue(key, out var occurrence))
        {
            _occurrences[key] = new Occurrence
            {
                RecordNumber = recordNumber,
                Geometry = _rule.ConfirmExactGeometry ? geometry : null,
                Count = 1
            };

            return 0;
        }

        // Misma huella y mismos atributos: se confirma la igualdad geométrica exacta
        // cuando la detección implica omitir registros.
        if (_rule.ConfirmExactGeometry
            && occurrence.Geometry is not null
            && !occurrence.Geometry.EqualsExact(geometry))
        {
            _fingerprintCollisions++;
            return 0;
        }

        occurrence.Count++;
        if (occurrence.Count == 2)
        {
            GroupCount++;
        }

        _redundantRecordNumbers.Add(recordNumber);
        return occurrence.RecordNumber;
    }

    private string BuildKey(Geometry geometry, IReadOnlyDictionary<string, string?> values)
    {
        var fingerprint = GeometryFingerprint.Compute(geometry);
        if (_rule.AttributeFields.Length == 0)
        {
            return fingerprint;
        }

        var builder = new StringBuilder(fingerprint);
        foreach (var field in _rule.AttributeFields)
        {
            builder.Append('\u001f');
            builder.Append(values.TryGetValue(field, out var value) ? value ?? "<nulo>" : "<sin campo>");
        }

        return builder.ToString();
    }
}
