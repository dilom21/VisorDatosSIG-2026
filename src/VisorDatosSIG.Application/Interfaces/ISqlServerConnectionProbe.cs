using VisorDatosSIG.Application.DTOs;

namespace VisorDatosSIG.Application.Interfaces;

public interface ISqlServerConnectionProbe
{
    Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken = default);
}
