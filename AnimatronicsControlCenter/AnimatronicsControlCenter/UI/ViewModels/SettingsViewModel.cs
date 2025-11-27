using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using System.IO.Ports;
using System.Threading.Tasks;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly ISerialService _serialService;

        [ObservableProperty]
        private string[] availablePorts;

        [ObservableProperty]
        private string selectedPort;

        [ObservableProperty]
        private int baudRate;

        [ObservableProperty]
        private bool isVirtualModeEnabled;

        public SettingsViewModel(ISettingsService settingsService, ISerialService serialService)
        {
            _settingsService = settingsService;
            _serialService = serialService;
            
            _settingsService.Load();
            SelectedPort = _settingsService.LastComPort;
            BaudRate = _settingsService.LastBaudRate;
            IsVirtualModeEnabled = _settingsService.IsVirtualModeEnabled;
            
            AvailablePorts = SerialPort.GetPortNames();
        }

        partial void OnIsVirtualModeEnabledChanged(bool value)
        {
             _settingsService.IsVirtualModeEnabled = value;
             _settingsService.Save();
             if (value)
             {
                 // Trigger connection to virtual devices?
                 // For now, let's just save. The user might need to click Connect or we can auto-connect.
                 // The requirement says "When toggle button is pressed ... it connects to virtual devices".
                 // So we should probably call ConnectAsync if value is true.
                 _ = ConnectAsync();
             }
             else 
             {
                 _serialService.Disconnect();
             }
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (string.IsNullOrEmpty(SelectedPort)) return;
            
            await _serialService.ConnectAsync(SelectedPort, BaudRate);
            _settingsService.LastComPort = SelectedPort;
            _settingsService.LastBaudRate = BaudRate;
            _settingsService.Save();
        }
    }
}

