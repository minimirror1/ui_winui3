using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.UI.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly ISerialService _serialService;
        private readonly IBackendDashboardSyncService _backendDashboardSyncService;
        private readonly DispatcherQueue _dispatcherQueue;

        [ObservableProperty]
        private bool isScanning;

        public ObservableCollection<Device> Devices { get; } = new();

        public DashboardViewModel(ISerialService serialService, IBackendDashboardSyncService backendDashboardSyncService)
        {
            _serialService = serialService;
            _backendDashboardSyncService = backendDashboardSyncService;
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

        private void ReplaceDashboardDevices(IEnumerable<Device> found)
        {
            void Apply()
            {
                Devices.Clear();
                foreach (var device in found)
                {
                    Devices.Add(device);
                }

                _backendDashboardSyncService.ReplaceDevices(Devices);
                _backendDashboardSyncService.Start();
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
    }
}
