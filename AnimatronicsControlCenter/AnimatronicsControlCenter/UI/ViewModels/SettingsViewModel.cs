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

        public SettingsViewModel(ISettingsService settingsService, ISerialService serialService)
        {
            _settingsService = settingsService;
            _serialService = serialService;
            
            _settingsService.Load();
            SelectedPort = _settingsService.LastComPort;
            BaudRate = _settingsService.LastBaudRate;
            
            AvailablePorts = SerialPort.GetPortNames();
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

