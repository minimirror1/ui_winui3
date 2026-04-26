using System;

namespace AnimatronicsControlCenter.Core.Utilities;

public static class DeviceStatusRefreshPolicy
{
    public const int DefaultIntervalSeconds = 5;

    public static bool ShouldRun(
        bool hasSelectedDevice,
        bool isInitialLoadInProgress,
        bool isPeriodicPingEnabled = true)
        => isPeriodicPingEnabled && hasSelectedDevice && !isInitialLoadInProgress;

    public static int GetIntervalMs(int intervalSeconds)
        => Math.Max(1, intervalSeconds) * 1000;
}
