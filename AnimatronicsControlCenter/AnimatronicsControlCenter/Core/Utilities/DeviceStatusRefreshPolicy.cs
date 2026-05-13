using System;

namespace AnimatronicsControlCenter.Core.Utilities;

public static class DeviceStatusRefreshPolicy
{
    public const double DefaultIntervalSeconds = 5;

    public static bool ShouldRun(
        bool hasSelectedDevice,
        bool isInitialLoadInProgress,
        bool isPeriodicPingEnabled = true)
        => isPeriodicPingEnabled && hasSelectedDevice && !isInitialLoadInProgress;

    public static int GetIntervalMs(double intervalSeconds)
        => (int)Math.Round(Math.Max(0.1, intervalSeconds) * 1000);
}
