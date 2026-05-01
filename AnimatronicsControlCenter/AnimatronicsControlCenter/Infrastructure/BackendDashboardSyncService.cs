using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendDashboardSyncService : IBackendDashboardSyncService
{
    private readonly ISerialService _serialService;
    private readonly IBackendMonitoringService _backendMonitoringService;
    private readonly ISettingsService _settingsService;
    private readonly object _lock = new();
    private List<Device> _devices = new();
    private CancellationTokenSource? _cts;

    public BackendDashboardSyncService(
        ISerialService serialService,
        IBackendMonitoringService backendMonitoringService,
        ISettingsService settingsService)
    {
        _serialService = serialService;
        _backendMonitoringService = backendMonitoringService;
        _settingsService = settingsService;
    }

    public void ReplaceDevices(IEnumerable<Device> devices)
    {
        lock (_lock)
        {
            _devices = devices.ToList();
        }
    }

    public void Start()
    {
        if (!_settingsService.IsBackendSyncEnabled)
        {
            return;
        }

        if (_cts is { IsCancellationRequested: false })
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await SyncOnceAsync(cancellationToken).ConfigureAwait(false);

            int intervalSeconds = Math.Max(1, _settingsService.BackendSyncIntervalSeconds);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }

    private async Task SyncOnceAsync(CancellationToken cancellationToken)
    {
        List<Device> snapshot;
        lock (_lock)
        {
            snapshot = _devices.ToList();
        }

        foreach (Device device in snapshot)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                Device logDevice = await _serialService.PingDeviceAsync(device.Id).ConfigureAwait(false)
                    ?? CreateDisconnectedDevice(device.Id);
                await _backendMonitoringService.SendObjectLogAsync(logDevice, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    private static Device CreateDisconnectedDevice(int deviceId)
    {
        var device = new Device(deviceId)
        {
            PowerStatus = "OFF",
            MotionState = MotionState.Stopped,
            StatusMessage = "Disconnected"
        };
        device.Motors.Add(new MotorState
        {
            Id = deviceId,
            Type = "DEVICE",
            Status = "Disconnected"
        });
        return device;
    }
}
