using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public partial class ScanDialogViewModel : ObservableObject
    {
        private readonly ISerialService _serialService;

        [ObservableProperty]
        private int startId = 1;

        [ObservableProperty]
        private int endId = 10;

        [ObservableProperty]
        private double progress;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotScanning))]
        private bool isScanning;

        public bool IsNotScanning => !IsScanning;

        public List<Device> FoundDevices { get; } = new();

        public event Action? RequestClose;

        public ScanDialogViewModel(ISerialService serialService)
        {
            _serialService = serialService;
        }

        [RelayCommand]
        private async Task StartScanAsync()
        {
            if (IsScanning) return;
            IsScanning = true;
            Progress = 0;
            FoundDevices.Clear();

            int total = EndId - StartId + 1;
            if (total <= 0) 
            {
                IsScanning = false;
                return;
            }

            // Perform scan
            for (int i = 0; i < total; i++)
            {
                int currentId = StartId + i;
                var device = await _serialService.PingDeviceAsync(currentId);
                if (device != null)
                {
                    FoundDevices.Add(device);
                }
                Progress = ((double)(i + 1) / total) * 100;
            }

            IsScanning = false;
            RequestClose?.Invoke();
        }
    }
}

