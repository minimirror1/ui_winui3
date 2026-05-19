using System;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class OperatingHoursAutoSyncService : IOperatingHoursAutoSyncService
{
    private readonly ISettingsService _settingsService;
    private readonly IOperatingHoursSource _source;
    private readonly IOperatingHoursDeviceSyncService _syncService;
    private readonly Func<DateTimeOffset> _nowProvider;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    public OperatingHoursAutoSyncService(
        ISettingsService settingsService,
        IOperatingHoursSource source,
        IOperatingHoursDeviceSyncService syncService)
        : this(settingsService, source, syncService, () => DateTimeOffset.Now)
    {
    }

    public OperatingHoursAutoSyncService(
        ISettingsService settingsService,
        IOperatingHoursSource source,
        IOperatingHoursDeviceSyncService syncService,
        Func<DateTimeOffset> nowProvider)
    {
        _settingsService = settingsService;
        _source = source;
        _syncService = syncService;
        _nowProvider = nowProvider;
    }

    public void Start()
    {
        if (_loopTask is { IsCompleted: false })
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var result = await _source.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Success || result.Schedule is null)
        {
            return;
        }

        await _syncService
            .SyncRangeAsync(_settingsService.ScanStartId, _settingsService.ScanEndId, result.Schedule, cancellationToken)
            .ConfigureAwait(false);
    }

    public static TimeSpan GetDelayToNextTopOfHour(DateTimeOffset now)
    {
        var nextHour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset).AddHours(1);
        return nextHour - now;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(cancellationToken).ConfigureAwait(false);
                await Task.Delay(GetDelayToNextTopOfHour(_nowProvider()), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                try
                {
                    await Task.Delay(GetDelayToNextTopOfHour(_nowProvider()), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
