namespace VisorDatosSIG.Infrastructure.Migration;

internal sealed class MigrationVerificationResult
{
    public required int DestinationRecords { get; init; }
    public required int NullGeometryRecords { get; init; }
    public required int WrongSridRecords { get; init; }
    public required int InvalidGeometryRecords { get; init; }
}
