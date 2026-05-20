using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Protocol;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.Infrastructure;
using AnimatronicsControlCenter.UI.Helpers;
using AnimatronicsControlCenter; // For App and MainWindow
using System;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public record LanguageOption(string Code, string Name);
    public record ThemeOption(string Value, string Name);

    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly ISerialService _serialService;
        private readonly ILocalizationService _localizationService;
        private readonly SerialMonitorWindowHost _serialMonitorWindowHost;
        private readonly XBeeService _xbeeService;
        private readonly DashboardViewModel _dashboardViewModel;

        public event EventHandler? ThemeRestartRequested;

        public LocalizedStrings Strings { get; }

        [ObservableProperty]
        private string[] availablePorts = System.Array.Empty<string>();

        [ObservableProperty]
        private SerialPortOption[] availablePortOptions = System.Array.Empty<SerialPortOption>();

        [ObservableProperty]
        private string selectedPort;

        [ObservableProperty]
        private int[] availableBaudRates = new[] { 9600, 14400, 19200, 38400, 57600, 115200 };

        [ObservableProperty]
        private int baudRate;

        [ObservableProperty]
        private bool isVirtualModeEnabled;

        [ObservableProperty]
        private bool isLastPortAutoConnectEnabled;

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
        [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
        [NotifyPropertyChangedFor(nameof(ConnectionStatusBrush))]
        private bool isConnectionActive;

        public SolidColorBrush ConnectionStatusBrush => IsConnectionActive
            ? new SolidColorBrush(Color.FromArgb(255, 76, 201, 128))
            : new SolidColorBrush(Color.FromArgb(255, 128, 128, 128));

        public string ConnectButtonText => IsConnectionActive ?
            _localizationService.GetString("Disconnect_Button") : 
            _localizationService.GetString("Connect_Text");

        public string ConnectionStatusText => IsConnectionActive ? 
            _localizationService.GetString("Status_Connected") : 
            _localizationService.GetString("Status_Disconnected");

        // XBee Connection Properties
        [ObservableProperty]
        private string selectedXBeePort = string.Empty;

        [ObservableProperty]
        private int xBeeBaudRate = 115200;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(XBeeConnectButtonText))]
        [NotifyPropertyChangedFor(nameof(XBeeConnectionStatusText))]
        private bool isXBeeConnected;

        public string XBeeConnectButtonText => IsXBeeConnected ? 
            _localizationService.GetString("Disconnect_Button") : 
            _localizationService.GetString("Connect_Text");

        public string XBeeConnectionStatusText => IsXBeeConnected ? 
            $"{_localizationService.GetString("Status_Connected")} (0x{_xbeeService.Address64:X16})" : 
            _localizationService.GetString("Status_Disconnected");

        public List<LanguageOption> Languages { get; } = new()
        {
            new LanguageOption("ko-KR", "한국어"),
            new LanguageOption("en-US", "English")
        };

        public List<ThemeOption> ThemeOptions { get; }

        [ObservableProperty]
        private LanguageOption selectedLanguage;

        [ObservableProperty]
        private ThemeOption? selectedThemeOption;

        [ObservableProperty]
        private double responseTimeoutSeconds;

        public List<PingTimeZoneOption> PingTimeZoneOptions { get; } = PingTimeZoneCatalog.All.ToList();

        [ObservableProperty]
        private PingTimeZoneOption? selectedPingTimeZoneOption;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PingPreviewText))]
        [NotifyPropertyChangedFor(nameof(PingPayloadPreviewText))]
        private bool isPeriodicPingEnabled;

        [ObservableProperty]
        private double pingIntervalSeconds;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PingPreviewText))]
        [NotifyPropertyChangedFor(nameof(PingPayloadPreviewText))]
        private string pingCountryCode = "KR";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PingPreviewText))]
        [NotifyPropertyChangedFor(nameof(PingPayloadPreviewText))]
        private int pingUtcOffsetMinutes;

        [ObservableProperty]
        private int scanStartId = 1;

        [ObservableProperty]
        private int scanEndId = 10;

        public string PingPreviewText
            => PingTimePayloadFactory.FormatPreview(PingCountryCode, PingUtcOffsetMinutes, DateTimeOffset.UtcNow);

        public string PingPayloadPreviewText
            => $"payload: {FormatPingPayloadPreview()}";

        private bool _isInitialized;
        private bool _isUpdatingPingSelection;
        private bool _isUpdatingScanRange;
        private string _lastLoadedComPortForAutoConnect = string.Empty;

        public SettingsViewModel(
            ISettingsService settingsService,
            ISerialService serialService,
            ILocalizationService localizationService,
            SerialMonitorWindowHost serialMonitorWindowHost,
            XBeeService xbeeService,
            DashboardViewModel dashboardViewModel)
        {
            _settingsService = settingsService;
            _serialService = serialService;
            _localizationService = localizationService;
            _serialMonitorWindowHost = serialMonitorWindowHost;
            _xbeeService = xbeeService;
            _dashboardViewModel = dashboardViewModel;
            Strings = new LocalizedStrings(_localizationService);
            ThemeOptions = new List<ThemeOption>
            {
                new(AppThemeHelper.DefaultTheme, _localizationService.GetString("Theme_Default")),
                new(AppThemeHelper.LightTheme, _localizationService.GetString("Theme_Light")),
                new(AppThemeHelper.DarkTheme, _localizationService.GetString("Theme_Dark"))
            };
            
            // Set dispatcher for UI callbacks
            _xbeeService.SetDispatcherQueue(DispatcherQueue.GetForCurrentThread());
            
            _settingsService.Load();
            SelectedPort = _settingsService.LastComPort;
            _lastLoadedComPortForAutoConnect = _settingsService.LastComPort;
            BaudRate = _settingsService.LastBaudRate == 0 ? 115200 : _settingsService.LastBaudRate;
            IsVirtualModeEnabled = _settingsService.IsVirtualModeEnabled;
            IsLastPortAutoConnectEnabled = _settingsService.IsLastPortAutoConnectEnabled;
            ResponseTimeoutSeconds = _settingsService.ResponseTimeoutSeconds;
            IsPeriodicPingEnabled = _settingsService.IsPeriodicPingEnabled;
            PingIntervalSeconds = _settingsService.PingIntervalSeconds;
            PingCountryCode = _settingsService.PingCountryCode;
            PingUtcOffsetMinutes = _settingsService.PingUtcOffsetMinutes;
            ScanStartId = _settingsService.ScanStartId;
            ScanEndId = _settingsService.ScanEndId;
            SelectedPingTimeZoneOption = PingTimeZoneCatalog.FindOrDefault(PingCountryCode, PingUtcOffsetMinutes);

            RefreshPorts();
            
            var currentCode = _localizationService.CurrentCulture.Name;
            SelectedLanguage = Languages.FirstOrDefault(l => l.Code.Equals(currentCode, System.StringComparison.OrdinalIgnoreCase)) 
                             ?? Languages.FirstOrDefault(l => l.Code == "ko-KR") 
                             ?? Languages.First();
            SelectedThemeOption = ThemeOptions.FirstOrDefault(theme =>
                theme.Value.Equals(_settingsService.Theme, StringComparison.OrdinalIgnoreCase))
                ?? ThemeOptions.First();
            
            IsConnectionActive = _serialService.IsConnected;
            IsXBeeConnected = _xbeeService.IsConnected;
            _isInitialized = true;
            StartLastPortAutoConnectIfEnabled();
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

        partial void OnSelectedThemeOptionChanged(ThemeOption? value)
        {
            if (!_isInitialized || value is null) return;

            _settingsService.Theme = value.Value;
            _settingsService.Save();

            if (App.Current.m_window is MainWindow mainWindow)
            {
                mainWindow.ApplyTheme();
            }

            _serialMonitorWindowHost.ApplyTheme();
            ThemeRestartRequested?.Invoke(this, EventArgs.Empty);
        }

        partial void OnIsVirtualModeEnabledChanged(bool value)
        {
             _settingsService.IsVirtualModeEnabled = value;
             _settingsService.Save();
             if (value)
             {
                 _ = ConnectCoreAsync(autoScanAfterConnect: false);
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

        partial void OnIsLastPortAutoConnectEnabledChanged(bool value)
        {
            if (!_isInitialized) return;
            _settingsService.IsLastPortAutoConnectEnabled = value;
            _settingsService.Save();
        }

        partial void OnResponseTimeoutSecondsChanged(double value)
        {
            if (!_isInitialized) return;
            var timeoutSeconds = Math.Clamp(Math.Round(value, 1), 0.1, 60);
            if (Math.Abs(value - timeoutSeconds) > 0.001)
            {
                ResponseTimeoutSeconds = timeoutSeconds;
                return;
            }

            _settingsService.ResponseTimeoutSeconds = timeoutSeconds;
            _settingsService.Save();
        }

        partial void OnIsPeriodicPingEnabledChanged(bool value)
        {
            if (!_isInitialized) return;
            _settingsService.IsPeriodicPingEnabled = value;
            _settingsService.Save();
        }

        partial void OnPingIntervalSecondsChanged(double value)
        {
            if (!_isInitialized) return;
            var intervalSeconds = Math.Clamp(Math.Round(value, 1), 0.1, 60);
            if (Math.Abs(value - intervalSeconds) > 0.001)
            {
                PingIntervalSeconds = intervalSeconds;
                return;
            }

            _settingsService.PingIntervalSeconds = intervalSeconds;
            _settingsService.Save();
        }

        partial void OnPingCountryCodeChanged(string value)
        {
            OnPropertyChanged(nameof(PingPreviewText));
            OnPropertyChanged(nameof(PingPayloadPreviewText));
            if (!_isInitialized || _isUpdatingPingSelection) return;
            if (string.IsNullOrWhiteSpace(value) || value.Length != 2) return;

            var normalized = PingTimeZoneCatalog.NormalizeCountryCodeOrDefault(value);
            if (value != normalized)
            {
                PingCountryCode = normalized;
                return;
            }

            _settingsService.PingCountryCode = normalized;
            var matchingOption = PingTimeZoneCatalog.GetOptionsForCountry(normalized).FirstOrDefault();
            if (matchingOption != null)
            {
                SelectedPingTimeZoneOption = matchingOption;
            }
            else
            {
                _settingsService.Save();
            }
        }

        partial void OnPingUtcOffsetMinutesChanged(int value)
        {
            if (!_isInitialized) return;
            _settingsService.PingUtcOffsetMinutes = value;
            _settingsService.Save();
        }

        partial void OnSelectedPingTimeZoneOptionChanged(PingTimeZoneOption? value)
        {
            if (!_isInitialized || value == null) return;

            _isUpdatingPingSelection = true;
            PingCountryCode = value.CountryCode.ToUpperInvariant();
            PingUtcOffsetMinutes = value.UtcOffsetMinutes;
            _isUpdatingPingSelection = false;

            _settingsService.PingCountryCode = PingCountryCode;
            _settingsService.PingUtcOffsetMinutes = PingUtcOffsetMinutes;
            _settingsService.Save();
            OnPropertyChanged(nameof(PingPreviewText));
            OnPropertyChanged(nameof(PingPayloadPreviewText));
        }

        partial void OnScanStartIdChanged(int value)
        {
            if (!_isInitialized || _isUpdatingScanRange) return;
            SaveNormalizedScanRange(value, ScanEndId);
        }

        partial void OnScanEndIdChanged(int value)
        {
            if (!_isInitialized || _isUpdatingScanRange) return;
            SaveNormalizedScanRange(ScanStartId, value);
        }

        [RelayCommand]
        private void RefreshPorts()
        {
            var portNames = SerialPort.GetPortNames();
            AvailablePorts = portNames;
            AvailablePortOptions = portNames
                .Select(portName => SerialPortDisplay.CreateOption(portName, deviceInfo: null))
                .OrderBy(option => option.PortName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (AvailablePortOptions.Any() &&
                string.IsNullOrEmpty(SelectedPort))
            {
                SelectedPort = AvailablePortOptions[0].PortName;
            }
        }

        private void StartLastPortAutoConnectIfEnabled()
        {
            if (!IsLastPortAutoConnectEnabled || IsVirtualModeEnabled || string.IsNullOrWhiteSpace(_lastLoadedComPortForAutoConnect))
            {
                return;
            }

            if (!AvailablePortOptions.Any(option => option.PortName.Equals(_lastLoadedComPortForAutoConnect, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            SelectedPort = _lastLoadedComPortForAutoConnect;
            _ = ConnectIfDisconnectedCoreAsync(autoScanAfterConnect: true);
        }

        [RelayCommand]
        private Task ConnectAsync()
            => ConnectCoreAsync(autoScanAfterConnect: false);

        private async Task ConnectCoreAsync(bool autoScanAfterConnect)
        {
            if (IsConnectionActive)
            {
                _serialService.Disconnect();
                IsConnectionActive = false;
                return;
            }

            await ConnectIfDisconnectedCoreAsync(autoScanAfterConnect);
        }

        private async Task ConnectIfDisconnectedCoreAsync(bool autoScanAfterConnect)
        {
            if (_serialService.IsConnected)
            {
                IsConnectionActive = true;
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

                if (autoScanAfterConnect)
                {
                    await _dashboardViewModel.ScanConfiguredRangeAsync(_settingsService.ScanStartId, _settingsService.ScanEndId);
                }
            }
            catch
            {
                IsConnectionActive = false;
                // Ideally show error message
            }
        }

        [RelayCommand]
        private void OpenSerialMonitor()
        {
            _serialMonitorWindowHost.Show();
        }

        [RelayCommand]
        private async Task XBeeConnectAsync()
        {
            if (IsXBeeConnected)
            {
                _xbeeService.Disconnect();
                IsXBeeConnected = false;
                return;
            }

            if (string.IsNullOrEmpty(SelectedXBeePort)) return;

            try
            {
                var success = await _xbeeService.ConnectAsync(SelectedXBeePort, XBeeBaudRate);
                IsXBeeConnected = success;
            }
            catch
            {
                IsXBeeConnected = false;
            }
        }

        private string FormatPingPayloadPreview()
        {
            var packet = BinarySerializer.EncodePing(
                BinaryProtocolConst.HostId,
                tarId: 1,
                PingTimePayloadFactory.Create(PingCountryCode, PingUtcOffsetMinutes, DateTimeOffset.UtcNow));
            var payload = packet.AsSpan(BinaryProtocolConst.RequestHeaderSize);
            return string.Join(" ", payload.ToArray().Select(b => b.ToString("X2")));
        }

        private void SaveNormalizedScanRange(int startId, int endId)
        {
            int clampedStart = Math.Clamp(startId, 1, 254);
            int clampedEnd = Math.Clamp(endId, 1, 254);
            int normalizedStart = Math.Min(clampedStart, clampedEnd);
            int normalizedEnd = Math.Max(clampedStart, clampedEnd);

            _isUpdatingScanRange = true;
            try
            {
                ScanStartId = normalizedStart;
                ScanEndId = normalizedEnd;
            }
            finally
            {
                _isUpdatingScanRange = false;
            }

            _settingsService.ScanStartId = normalizedStart;
            _settingsService.ScanEndId = normalizedEnd;
            _settingsService.Save();
        }
    }
}

