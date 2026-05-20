using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class OperatingHoursDeviceSyncService : IOperatingHoursDeviceSyncService
{
    private readonly ISerialService _serialService;
    private readonly ISettingsService _settingsService;

    public OperatingHoursDeviceSyncService(ISerialService serialService, ISettingsService settingsService)
    {
        _serialService = serialService;
        _settingsService = settingsService;
    }

    public async Task<IReadOnlyList<OperatingHoursDeviceWriteResult>> SyncRangeAsync(
        int startDeviceId,
        int endDeviceId,
        OperatingHoursSchedule schedule,
        CancellationToken cancellationToken)
    {
        int start = Math.Min(startDeviceId, endDeviceId);
        int end = Math.Max(startDeviceId, endDeviceId);
        var results = new List<OperatingHoursDeviceWriteResult>();

        for (int deviceId = start; deviceId <= end; deviceId++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                results.Add(await _serialService.SetOperatingHoursAsync(deviceId, schedule).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                results.Add(new OperatingHoursDeviceWriteResult(deviceId, false, 0, ex.Message));
            }
        }

        return results;
    }
}
