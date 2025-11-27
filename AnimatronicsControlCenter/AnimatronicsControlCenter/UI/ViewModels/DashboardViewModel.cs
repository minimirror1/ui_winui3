using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using Microsoft.UI.Dispatching;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly ISerialService _serialService;
        private readonly DispatcherQueue _dispatcherQueue;

        [ObservableProperty]
        private bool isScanning;

        public ObservableCollection<Device> Devices { get; } = new();

        public DashboardViewModel(ISerialService serialService)
        {
            _serialService = serialService;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        [RelayCommand]
        private async Task ScanAsync()
        {
            if (IsScanning) return;
            IsScanning = true;
            Devices.Clear();

            try
            {
                var devices = await _serialService.ScanDevicesAsync(1, 10);
                foreach (var device in devices)
                {
                    Devices.Add(device);
                }
            }
            finally
            {
                IsScanning = false;
            }
        }
    }
}

