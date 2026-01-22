using Windows.Storage;
using AnimatronicsControlCenter.Core.Interfaces;

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

        public string LastComPort { get; set; } = "COM1";
        public int LastBaudRate { get; set; } = 115200;
        public string Theme { get; set; } = "Default";
        public bool IsVirtualModeEnabled { get; set; } = false;
        public string Language { get; set; } = "ko-KR";
        public double ResponseTimeoutSeconds { get; set; } = 1.0;

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
            }
            catch { /* Ignore if ApplicationData is not available (e.g. unpackaged) */ }
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
                if (localSettings.Values.TryGetValue(KeyResponseTimeout, out var timeout)) ResponseTimeoutSeconds = (int)timeout;
            }
            catch { /* Ignore if ApplicationData is not available (e.g. unpackaged) */ }
        }
    }
}

