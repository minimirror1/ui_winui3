using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.OperatingHours;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IOperatingHoursDeviceSyncService
{
    Task<IReadOnlyList<OperatingHoursDeviceWriteResult>> SyncRangeAsync(
        int startDeviceId,
        int endDeviceId,
        OperatingHoursSchedule schedule,
        CancellationToken cancellationToken);
}
