using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.OperatingHours;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IOperatingHoursSource
{
    Task<OperatingHoursSourceResult> LoadAsync(CancellationToken cancellationToken);
    Task<OperatingHoursSourceResult> SaveAsync(OperatingHoursSchedule schedule, CancellationToken cancellationToken);
}

public sealed record OperatingHoursSourceResult(
    bool Success,
    bool FromCache,
    string Message,
    OperatingHoursSchedule? Schedule);
