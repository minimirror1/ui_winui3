using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Utilities;
#if WINDOWS
using Windows.Storage;
#endif

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
        private readonly IBackendSettingsPathProvider _backendSettingsPathProvider;
        private static readonly JsonSerializerOptions BackendJsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        public SettingsService()
            : this(new BackendSettingsPathProvider())
        {
        }

        public SettingsService(IBackendSettingsPathProvider backendSettingsPathProvider)
        {
            _backendSettingsPathProvider = backendSettingsPathProvider;
        }

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
        public bool IsBackendSyncEnabled { get; set; } = true;
        public string BackendBaseUrl { get; set; } = "https://robot-monitor-api-dev.innergm.com";
        public string BackendBearerToken { get; set; } = string.Empty;
        public string BackendStoreId { get; set; } = string.Empty;
        public string BackendStoreName { get; set; } = string.Empty;
        public string BackendStoreCountryCode { get; set; } = string.Empty;
        public string BackendPcId { get; set; } = string.Empty;
        public string BackendPcName { get; set; } = "pc_name_001";
        public string BackendSoftwareVersion { get; set; } = "1.1.1.0";
        public Dictionary<int, string> BackendDeviceObjectMappings { get; set; } = new();
        public int BackendSyncIntervalSeconds { get; set; } = 5;

        public void Save()
        {
#if WINDOWS
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
#endif

            SaveBackendSettings();
        }

        public void Load()
        {
#if WINDOWS
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
#endif

            LoadBackendSettings();
        }

        private void SaveBackendSettings()
        {
            try
            {
                string? directory = Path.GetDirectoryName(_backendSettingsPathProvider.BackendSettingsFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var settings = new BackendSettingsFile(
                    IsBackendSyncEnabled,
                    BackendBaseUrl,
                    BackendBearerToken,
                    BackendStoreId,
                    BackendStoreName,
                    BackendStoreCountryCode,
                    BackendPcId,
                    BackendPcName,
                    BackendSoftwareVersion,
                    BackendDeviceObjectMappings,
                    BackendSyncIntervalSeconds);

                string json = JsonSerializer.Serialize(settings, BackendJsonOptions);
                File.WriteAllText(_backendSettingsPathProvider.BackendSettingsFilePath, json);
            }
            catch
            {
            }
        }

        private void LoadBackendSettings()
        {
            try
            {
                if (!File.Exists(_backendSettingsPathProvider.BackendSettingsFilePath))
                {
                    return;
                }

                string json = File.ReadAllText(_backendSettingsPathProvider.BackendSettingsFilePath);
                var settings = JsonSerializer.Deserialize<BackendSettingsFile>(json, BackendJsonOptions);
                if (settings is null)
                {
                    return;
                }

                IsBackendSyncEnabled = settings.IsBackendSyncEnabled;
                BackendBaseUrl = settings.BackendBaseUrl ?? BackendBaseUrl;
                BackendBearerToken = settings.BackendBearerToken ?? BackendBearerToken;
                BackendStoreId = settings.BackendStoreId ?? BackendStoreId;
                BackendStoreName = settings.BackendStoreName ?? BackendStoreName;
                BackendStoreCountryCode = settings.BackendStoreCountryCode ?? BackendStoreCountryCode;
                BackendPcId = settings.BackendPcId ?? BackendPcId;
                BackendPcName = settings.BackendPcName ?? BackendPcName;
                BackendSoftwareVersion = settings.BackendSoftwareVersion ?? BackendSoftwareVersion;
                BackendDeviceObjectMappings = settings.BackendDeviceObjectMappings ?? new Dictionary<int, string>();
                BackendSyncIntervalSeconds = settings.BackendSyncIntervalSeconds;
            }
            catch
            {
            }
        }

        private sealed record BackendSettingsFile(
            bool IsBackendSyncEnabled,
            string BackendBaseUrl,
            string BackendBearerToken,
            string BackendStoreId,
            string BackendStoreName,
            string BackendStoreCountryCode,
            string BackendPcId,
            string BackendPcName,
            string BackendSoftwareVersion,
            Dictionary<int, string> BackendDeviceObjectMappings,
            int BackendSyncIntervalSeconds);
    }
}
