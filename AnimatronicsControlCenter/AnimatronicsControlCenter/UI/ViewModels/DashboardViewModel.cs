using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.UI.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly ISerialService _serialService;
        private readonly IBackendDashboardSyncService _backendDashboardSyncService;
        private readonly ISettingsService _settingsService;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly object _devicesLock = new();
        private List<Device> _statusDevices = new();
        private CancellationTokenSource? _statusPollingCts;
        private Task? _statusPollingTask;

        [ObservableProperty]
        private bool isScanning;

        public ObservableCollection<Device> Devices { get; } = new();

        public DashboardViewModel(ISerialService serialService, IBackendDashboardSyncService backendDashboardSyncService, ISettingsService settingsService)
        {
            _serialService = serialService;
            _backendDashboardSyncService = backendDashboardSyncService;
            _settingsService = settingsService;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        [RelayCommand]
        private async Task ScanAsync()
        {
            if (IsScanning) return;
            IsScanning = true;

            try
            {
                var dialog = new ScanDialog();
                if (App.Current.m_window?.Content is FrameworkElement element)
                {
                    dialog.XamlRoot = element.XamlRoot;
                }

                await dialog.ShowAsync();

                var found = dialog.ViewModel.FoundDevices;

                ReplaceDashboardDevices(found);
            }
            finally
            {
                IsScanning = false;
            }
        }

        public async Task ScanConfiguredRangeAsync(int startId, int endId)
        {
            if (IsScanning) return;
            IsScanning = true;

            try
            {
                var found = await _serialService.ScanDevicesAsync(startId, endId);
                ReplaceDashboardDevices(found);
            }
            finally
            {
                IsScanning = false;
            }
        }

        private void ApplyDeviceNames(IEnumerable<Device> devices)
        {
            var mappings = _settingsService.BackendDeviceObjectMappings;
            var serverObjects = _settingsService.BackendServerObjects
                .ToDictionary(o => o.ObjectId, o => o.ObjectName ?? string.Empty);

            foreach (var device in devices)
            {
                if (mappings.TryGetValue(device.Id, out var objectId) &&
                    serverObjects.TryGetValue(objectId, out var name))
                {
                    device.Name = name;
                }
            }
        }

        private void ReplaceDashboardDevices(IEnumerable<Device> found)
        {
            void Apply()
            {
                Devices.Clear();
                var list = found.ToList();
                ApplyDeviceNames(list);
                lock (_devicesLock)
                {
                    _statusDevices = list;
                }

                foreach (var device in list)
                {
                    Devices.Add(device);
                }

                _backendDashboardSyncService.ReplaceDevices(Devices);
                _backendDashboardSyncService.Start();
                EnsureStatusPolling();
            }

            if (_dispatcherQueue.HasThreadAccess)
            {
                Apply();
            }
            else
            {
                _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, Apply);
            }
        }

        private void EnsureStatusPolling()
        {
            if (!_settingsService.IsPeriodicPingEnabled || _statusDevices.Count == 0)
            {
                StopStatusPolling();
                return;
            }

            if (_statusPollingTask is { IsCompleted: false })
            {
                return;
            }

            _statusPollingCts = new CancellationTokenSource();
            _statusPollingTask = RunStatusPollingLoopAsync(_statusPollingCts.Token);
        }

        private void StopStatusPolling()
        {
            _statusPollingCts?.Cancel();
            _statusPollingCts?.Dispose();
            _statusPollingCts = null;
            _statusPollingTask = null;
        }

        private async Task RunStatusPollingLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(
                        DeviceStatusRefreshPolicy.GetIntervalMs(_settingsService.PingIntervalSeconds),
                        token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!_settingsService.IsPeriodicPingEnabled)
                {
                    StopStatusPolling();
                    return;
                }

                List<Device> snapshot;
                lock (_devicesLock)
                {
                    snapshot = _statusDevices.ToList();
                }

                foreach (Device device in snapshot)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    Device? refreshedDevice = await _serialService.PingDeviceAsync(device.Id).ConfigureAwait(false);
                    UpdateDeviceStatus(device, refreshedDevice);
                }
            }
        }

        private void UpdateDeviceStatus(Device target, Device? source)
        {
            void Apply()
            {
                if (source is null)
                {
                    ApplyDisconnectedStatus(target);
                }
                else
                {
                    ApplyDeviceStatus(target, source);
                }
            }

            if (_dispatcherQueue.HasThreadAccess)
            {
                Apply();
            }
            else
            {
                _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, Apply);
            }
        }

        private static void ApplyDeviceStatus(Device target, Device source)
        {
            target.IsConnected = source.IsConnected;
            target.StatusMessage = source.StatusMessage;
            target.MotionState = source.MotionState;
            target.MotionCurrentTime = source.MotionCurrentTime;
            target.MotionTotalTime = source.MotionTotalTime;
            target.Address64 = source.Address64;
            target.PowerStatus = source.PowerStatus;
            target.HasError = source.HasError;
        }

        private static void ApplyDisconnectedStatus(Device target)
        {
            target.IsConnected = false;
            target.PowerStatus = "OFF";
            target.HasError = false;
            target.MotionState = MotionState.Stopped;
            target.StatusMessage = "Disconnected";
            target.Motors.Clear();
            target.Motors.Add(new MotorState
            {
                Id = target.Id,
                Type = "DEVICE",
                Status = "Disconnected"
            });
        }
    }
}
