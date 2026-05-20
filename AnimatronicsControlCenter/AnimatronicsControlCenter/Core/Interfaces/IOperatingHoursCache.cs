using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.OperatingHours;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IOperatingHoursCache
{
    Task SaveAsync(OperatingHoursSchedule schedule, CancellationToken cancellationToken);
    Task<OperatingHoursSchedule?> LoadAsync(CancellationToken cancellationToken);
}
