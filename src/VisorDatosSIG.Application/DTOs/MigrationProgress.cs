namespace VisorDatosSIG.Application.DTOs;

public sealed class MigrationProgress
{
    public required ShapefileLayer Layer { get; init; }
    public required string Phase { get; init; }
    public required int TotalRecords { get; init; }
    public required int ProcessedRecords { get; init; }
    public required int InsertedRecords { get; init; }
    public required int OmittedRecords { get; init; }
    public required int FailedRecords { get; init; }
    public required int CurrentBatch { get; init; }
    public required int TotalBatches { get; init; }

    public int Percentage => TotalRecords <= 0
        ? 0
        : Math.Clamp((int)Math.Round(ProcessedRecords * 100d / TotalRecords), 0, 100);
}
