using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Utilities;
using Windows.Storage;

namespace AnimatronicsControlCenter.Infrastructure
{
    public class SettingsService : ISettingsService
    {
        private const string KeyComPort = "ComPort";
        private const string KeyBaudRate = "BaudRate";
        private const string KeyTheme = "Theme";
        private const string KeyIsVirtualModeEnabled = "IsVirtualModeEnabled";
        private const string KeyLanguage = "Language";
        private const string KeyResponseTimeout = "ResponseTimeoutSeconds";
        private const string KeyIsPeriodicPingEnabled = "IsPeriodicPingEnabled";
        private const string KeyPingIntervalSeconds = "PingIntervalSeconds";
        private const string KeyPingCountryCode = "PingCountryCode";
        private const string KeyPingUtcOffsetMinutes = "PingUtcOffsetMinutes";

        public string LastComPort { get; set; } = "COM1";
        public int LastBaudRate { get; set; } = 115200;
        public string Theme { get; set; } = "Default";
        public bool IsVirtualModeEnabled { get; set; } = false;
        public string Language { get; set; } = "ko-KR";
        public double ResponseTimeoutSeconds { get; set; } = 2.0;
        public bool IsPeriodicPingEnabled { get; set; } = true;
        public int PingIntervalSeconds { get; set; } = 5;
        public string PingCountryCode { get; set; } = "KR";
        public int PingUtcOffsetMinutes { get; set; } = 540;

        public void Save()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[KeyComPort] = LastComPort;
                localSettings.Values[KeyBaudRate] = LastBaudRate;
                localSettings.Values[KeyTheme] = Theme;
                localSettings.Values[KeyIsVirtualModeEnabled] = IsVirtualModeEnabled;
                localSettings.Values[KeyLanguage] = Language;
                localSettings.Values[KeyResponseTimeout] = ResponseTimeoutSeconds;
                localSettings.Values[KeyIsPeriodicPingEnabled] = IsPeriodicPingEnabled;
                localSettings.Values[KeyPingIntervalSeconds] = PingIntervalSeconds;
                localSettings.Values[KeyPingCountryCode] = PingCountryCode;
                localSettings.Values[KeyPingUtcOffsetMinutes] = PingUtcOffsetMinutes;
            }
            catch
            {
            }
        }

        public void Load()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                if (localSettings.Values.TryGetValue(KeyComPort, out var port)) LastComPort = (string)port;
                if (localSettings.Values.TryGetValue(KeyBaudRate, out var rate)) LastBaudRate = (int)rate;
                if (localSettings.Values.TryGetValue(KeyTheme, out var theme)) Theme = (string)theme;
                if (localSettings.Values.TryGetValue(KeyIsVirtualModeEnabled, out var isVirtual)) IsVirtualModeEnabled = (bool)isVirtual;
                if (localSettings.Values.TryGetValue(KeyLanguage, out var lang)) Language = (string)lang;
                if (localSettings.Values.TryGetValue(KeyResponseTimeout, out var timeout))
                {
                    ResponseTimeoutSeconds = SettingValueConverter.ReadDouble(timeout, ResponseTimeoutSeconds);
                }
                if (localSettings.Values.TryGetValue(KeyIsPeriodicPingEnabled, out var pingEnabled)) IsPeriodicPingEnabled = (bool)pingEnabled;
                if (localSettings.Values.TryGetValue(KeyPingIntervalSeconds, out var pingInterval)) PingIntervalSeconds = (int)pingInterval;
                if (localSettings.Values.TryGetValue(KeyPingCountryCode, out var pingCountryCode)) PingCountryCode = (string)pingCountryCode;
                if (localSettings.Values.TryGetValue(KeyPingUtcOffsetMinutes, out var pingOffset)) PingUtcOffsetMinutes = (int)pingOffset;
            }
            catch
            {
            }
        }
    }
}
