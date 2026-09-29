using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Application.Interfaces;

public interface IMigrationService
{
    Task<MigrationResult> MigrateAsync(
        MigrationOptions options,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
