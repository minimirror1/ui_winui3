using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Backend;
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
                if (result.Data.OperateTimes is null || result.Data.OperateTimes.Count == 0)
                {
                    return await LoadCacheOrFailureAsync("Backend store detail does not contain operate_times.", cancellationToken);
                }

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

    public async Task<OperatingHoursSourceResult> SaveAsync(
        OperatingHoursSchedule schedule,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        if (string.IsNullOrWhiteSpace(_settingsService.BackendStoreId))
        {
            return new OperatingHoursSourceResult(false, false, "Backend store id is not selected.", null);
        }

        try
        {
            var detailResult = await _catalogClient
                .GetStoreDetailAsync(_settingsService.BackendStoreId, cancellationToken)
                .ConfigureAwait(false);

            if (!detailResult.Success || detailResult.Data is null)
            {
                return new OperatingHoursSourceResult(false, false, detailResult.Message, null);
            }

            var detail = detailResult.Data;
            var request = new BackendStoreUpdateRequest(
                detail.StoreName,
                detail.CountryCode,
                detail.Address,
                detail.Latitude,
                detail.Longitude,
                detail.Timezone,
                schedule.Days
                    .Select(day => new BackendStoreOperateTime(
                        day.DayOfWeek,
                        FormatMinutes(day.OpenMinutes),
                        FormatMinutes(day.CloseMinutes)))
                    .ToArray());

            var updateResult = await _catalogClient
                .UpdateStoreAsync(_settingsService.BackendStoreId, request, cancellationToken)
                .ConfigureAwait(false);

            if (!updateResult.Success)
            {
                return new OperatingHoursSourceResult(false, false, updateResult.Message, null);
            }

            return await LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new OperatingHoursSourceResult(false, false, ex.Message, null);
        }
    }

    private async Task<OperatingHoursSourceResult> LoadCacheOrFailureAsync(string message, CancellationToken cancellationToken)
    {
        var cached = await _cache.LoadAsync(cancellationToken).ConfigureAwait(false);
        return cached is null
            ? new OperatingHoursSourceResult(false, false, message, null)
            : new OperatingHoursSourceResult(true, true, "Loaded operating hours from local cache.", cached);
    }

    private static string FormatMinutes(ushort minutes)
        => TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm", CultureInfo.InvariantCulture);
}
