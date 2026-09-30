using NetTopologySuite.Geometries;

namespace VisorDatosSIG.Infrastructure.Migration;

internal sealed class MigrationRow
{
    public required IReadOnlyDictionary<string, object?> Values { get; init; }
    public required Geometry Geometry { get; init; }
}
