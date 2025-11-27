using System;
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
            
            try
            {
                var dialog = new ScanDialog();
                if (App.Current.m_window?.Content is FrameworkElement element)
                {
                    dialog.XamlRoot = element.XamlRoot;
                }
                
                await dialog.ShowAsync();
                
                var found = dialog.ViewModel.FoundDevices;
                
                Devices.Clear();
                foreach (var device in found)
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
