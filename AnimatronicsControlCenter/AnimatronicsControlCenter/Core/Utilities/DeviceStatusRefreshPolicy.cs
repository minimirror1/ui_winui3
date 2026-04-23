namespace AnimatronicsControlCenter.Core.Utilities;

public static class DeviceStatusRefreshPolicy
{
    public const int IntervalMs = 1000;

    public static bool ShouldRun(bool hasSelectedDevice, bool isInitialLoadInProgress)
        => hasSelectedDevice && !isInitialLoadInProgress;
}
