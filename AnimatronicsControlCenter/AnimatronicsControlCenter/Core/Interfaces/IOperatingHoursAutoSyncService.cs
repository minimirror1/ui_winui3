using System.Threading;
using System.Threading.Tasks;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IOperatingHoursAutoSyncService
{
    void Start();
    void Stop();
    Task RunOnceAsync(CancellationToken cancellationToken);
}
