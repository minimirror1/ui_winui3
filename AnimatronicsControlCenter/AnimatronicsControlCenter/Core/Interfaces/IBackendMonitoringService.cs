using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendMonitoringService
{
    Task<BackendSendResult> SendObjectLogAsync(Device device, CancellationToken cancellationToken);
}

public sealed record BackendSendResult(bool Success, int? StatusCode, string Message);
