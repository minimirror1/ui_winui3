using Windows.Storage;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.Infrastructure
{
    public class SettingsService : ISettingsService
    {
        private const string KeyComPort = "ComPort";
        private const string KeyBaudRate = "BaudRate";
        private const string KeyTheme = "Theme";

        public string LastComPort { get; set; } = "COM1";
        public int LastBaudRate { get; set; } = 9600;
        public string Theme { get; set; } = "Default";

        public void Save()
        {
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[KeyComPort] = LastComPort;
                localSettings.Values[KeyBaudRate] = LastBaudRate;
                localSettings.Values[KeyTheme] = Theme;
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
            }
            catch { /* Ignore if ApplicationData is not available (e.g. unpackaged) */ }
        }
    }
}

