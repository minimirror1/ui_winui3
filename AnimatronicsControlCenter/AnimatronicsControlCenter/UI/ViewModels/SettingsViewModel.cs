using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.UI.Helpers;
using AnimatronicsControlCenter; // For App and MainWindow
using System.IO.Ports;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public record LanguageOption(string Code, string Name);

    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly ISerialService _serialService;
        private readonly ILocalizationService _localizationService;

        public LocalizedStrings Strings { get; }

        [ObservableProperty]
        private string[] availablePorts = System.Array.Empty<string>();

        [ObservableProperty]
        private string selectedPort;

        [ObservableProperty]
        private int[] availableBaudRates = new[] { 9600, 14400, 19200, 38400, 57600, 115200 };

        [ObservableProperty]
        private int baudRate;

        [ObservableProperty]
        private bool isVirtualModeEnabled;

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
        [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
        private bool isConnectionActive;

        public string ConnectButtonText => IsConnectionActive ? 
            _localizationService.GetString("Disconnect_Button") : 
            _localizationService.GetString("Connect_Text");

        public string ConnectionStatusText => IsConnectionActive ? 
            _localizationService.GetString("Status_Connected") : 
            _localizationService.GetString("Status_Disconnected");

        public List<LanguageOption> Languages { get; } = new()
        {
            new LanguageOption("en-US", "English"),
            new LanguageOption("ko-KR", "한국어")
        };

        [ObservableProperty]
        private LanguageOption selectedLanguage;

        private bool _isInitialized;

        public SettingsViewModel(ISettingsService settingsService, ISerialService serialService, ILocalizationService localizationService)
        {
            _settingsService = settingsService;
            _serialService = serialService;
            _localizationService = localizationService;
            Strings = new LocalizedStrings(_localizationService);
            
            _settingsService.Load();
            SelectedPort = _settingsService.LastComPort;
            BaudRate = _settingsService.LastBaudRate == 0 ? 115200 : _settingsService.LastBaudRate;
            IsVirtualModeEnabled = _settingsService.IsVirtualModeEnabled;
            
            RefreshPorts();
            
            var currentCode = _localizationService.CurrentCulture.Name;
            SelectedLanguage = Languages.FirstOrDefault(l => l.Code.Equals(currentCode, System.StringComparison.OrdinalIgnoreCase)) 
                             ?? Languages.FirstOrDefault(l => l.Code == "en-US") 
                             ?? Languages.First();
            
            IsConnectionActive = _serialService.IsConnected;
            _isInitialized = true;
        }

        partial void OnSelectedLanguageChanged(LanguageOption value)
        {
            if (!_isInitialized) return;

            if (value != null)
            {
                _localizationService.SetLanguage(value.Code);
                
                _settingsService.Language = value.Code;
                _settingsService.Save();

                // Refresh localized strings
                OnPropertyChanged(nameof(ConnectButtonText));
                OnPropertyChanged(nameof(ConnectionStatusText));

                if (App.Current.m_window is MainWindow mainWindow)
                {
                    mainWindow.UpdateLanguage();
                }
            }
        }

        partial void OnIsVirtualModeEnabledChanged(bool value)
        {
             _settingsService.IsVirtualModeEnabled = value;
             _settingsService.Save();
             if (value)
             {
                 _ = ConnectAsync();
             }
             else 
             {
                 if (IsConnectionActive)
                 {
                    _serialService.Disconnect();
                    IsConnectionActive = false;
                 }
             }
        }

        [RelayCommand]
        private void RefreshPorts()
        {
            AvailablePorts = SerialPort.GetPortNames();
            if (AvailablePorts.Any() && (string.IsNullOrEmpty(SelectedPort) || !AvailablePorts.Contains(SelectedPort)))
            {
                SelectedPort = AvailablePorts[0];
            }
        }

        [RelayCommand]
        private async Task ConnectAsync()
        {
            if (IsConnectionActive)
            {
                _serialService.Disconnect();
                IsConnectionActive = false;
                return;
            }

            if (string.IsNullOrEmpty(SelectedPort) && !IsVirtualModeEnabled) return;
            
            try
            {
                await _serialService.ConnectAsync(SelectedPort, BaudRate);
                IsConnectionActive = true;
                
                _settingsService.LastComPort = SelectedPort;
                _settingsService.LastBaudRate = BaudRate;
                _settingsService.Save();
            }
            catch
            {
                IsConnectionActive = false;
                // Ideally show error message
            }
        }
    }
}
