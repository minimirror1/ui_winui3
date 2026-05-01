using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendObjectIdResolver : IBackendObjectIdResolver
{
    private readonly ISettingsService _settingsService;

    public BackendObjectIdResolver(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public string? ResolveObjectId(int deviceId)
    {
        if (!_settingsService.BackendDeviceObjectMappings.TryGetValue(deviceId, out string? objectId))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(objectId) ? null : objectId;
    }
}
