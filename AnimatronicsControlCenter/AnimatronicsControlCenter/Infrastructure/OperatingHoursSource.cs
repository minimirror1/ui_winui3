using System;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class OperatingHoursSource : IOperatingHoursSource
{
    private readonly ISettingsService _settingsService;
    private readonly IBackendServerCatalogClient _catalogClient;
    private readonly IOperatingHoursCache _cache;

    public OperatingHoursSource(
        ISettingsService settingsService,
        IBackendServerCatalogClient catalogClient,
        IOperatingHoursCache cache)
    {
        _settingsService = settingsService;
        _catalogClient = catalogClient;
        _cache = cache;
    }

    public async Task<OperatingHoursSourceResult> LoadAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settingsService.BackendStoreId))
        {
            return new OperatingHoursSourceResult(false, false, "Backend store id is not selected.", null);
        }

        try
        {
            var result = await _catalogClient
                .GetStoreDetailAsync(_settingsService.BackendStoreId, cancellationToken)
                .ConfigureAwait(false);

            if (result.Success && result.Data is not null)
            {
                var schedule = OperatingHoursSchedule.FromStoreDetail(result.Data);
                await _cache.SaveAsync(schedule, cancellationToken).ConfigureAwait(false);
                return new OperatingHoursSourceResult(true, false, "Loaded operating hours from server.", schedule);
            }

            return await LoadCacheOrFailureAsync(result.Message, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return await LoadCacheOrFailureAsync(ex.Message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<OperatingHoursSourceResult> LoadCacheOrFailureAsync(string message, CancellationToken cancellationToken)
    {
        var cached = await _cache.LoadAsync(cancellationToken).ConfigureAwait(false);
        return cached is null
            ? new OperatingHoursSourceResult(false, false, message, null)
            : new OperatingHoursSourceResult(true, true, "Loaded operating hours from local cache.", cached);
    }
}
