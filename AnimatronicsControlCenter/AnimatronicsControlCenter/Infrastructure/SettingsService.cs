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

        public string LastComPort { get; set; } = "COM1";
        public int LastBaudRate { get; set; } = 9600;
        public string Theme { get; set; } = "Default";
        public bool IsVirtualModeEnabled { get; set; } = false;
        public string Language { get; set; } = "en-US";

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
            }
            catch { /* Ignore if ApplicationData is not available (e.g. unpackaged) */ }
        }
    }
}

