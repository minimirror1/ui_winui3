using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendDashboardSyncService
{
    void ReplaceDevices(IEnumerable<Device> devices);
    void Start();
    void Stop();
}
