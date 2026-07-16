using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Utilities;
#if WINDOWS
using Windows.Storage;
#endif

namespace AnimatronicsControlCenter.Infrastructure
{
    public class SettingsService : ISettingsService
    {
        private const string AppSettingsFileName = "app-settings.json";
        private const string KeyComPort = "ComPort";
        private const string KeyBaudRate = "BaudRate";
        private const string KeyTheme = "Theme";
        private const string KeyIsVirtualModeEnabled = "IsVirtualModeEnabled";
        private const string KeyIsLastPortAutoConnectEnabled = "IsLastPortAutoConnectEnabled";
        private const string KeyLanguage = "Language";
        private const string KeyResponseTimeout = "ResponseTimeoutSeconds";
        private const string KeyIsPeriodicPingEnabled = "IsPeriodicPingEnabled";
        private const string KeyPingIntervalSeconds = "PingIntervalSeconds";
        private const string KeyPingCountryCode = "PingCountryCode";
        private const string KeyPingUtcOffsetMinutes = "PingUtcOffsetMinutes";
        private const string ThemeDefault = "Default";
        private const string ThemeLight = "Light";
        private const string ThemeDark = "Dark";
        private const int MinScanId = 1;
        private const int MaxScanId = 254;
        private readonly IBackendSettingsPathProvider _backendSettingsPathProvider;
        private readonly IBackendApiKeyStore _backendApiKeyStore;
        private string _theme = ThemeDefault;
        private static readonly JsonSerializerOptions BackendJsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        public SettingsService()
            : this(new BackendSettingsPathProvider(), new BackendApiKeyStore())
        {
        }

        public SettingsService(IBackendSettingsPathProvider backendSettingsPathProvider)
            : this(backendSettingsPathProvider, new BackendApiKeyStore())
        {
        }

        public SettingsService(
            IBackendSettingsPathProvider backendSettingsPathProvider,
            IBackendApiKeyStore backendApiKeyStore)
        {
            _backendSettingsPathProvider = backendSettingsPathProvider;
            _backendApiKeyStore = backendApiKeyStore;
        }

        public string LastComPort { get; set; } = "COM1";
        public int LastBaudRate { get; set; } = 115200;
        public string Theme
        {
            get => _theme;
            set => _theme = NormalizeTheme(value);
        }
        public bool IsVirtualModeEnabled { get; set; } = false;
        public bool IsLastPortAutoConnectEnabled { get; set; } = false;
        public string Language { get; set; } = "ko-KR";
        public double ResponseTimeoutSeconds { get; set; } = 2.0;
        public bool IsPeriodicPingEnabled { get; set; } = true;
        public double PingIntervalSeconds { get; set; } = 5;
        public string PingCountryCode { get; set; } = "KR";
        public int PingUtcOffsetMinutes { get; set; } = 540;
        public int ScanStartId { get; set; } = 1;
        public int ScanEndId { get; set; } = 10;
        public string AppSettingsFilePath => GetAppSettingsFilePath();
        public bool IsBackendSyncEnabled { get; set; } = true;
        public string BackendBaseUrl { get; set; } = "https://robot-monitor-api.innergm.com";
        public string BackendBearerToken { get; set; } = string.Empty;
        public string BackendApiKey { get; set; } = string.Empty;
        public string BackendStoreId { get; set; } = string.Empty;
        public string BackendStoreName { get; set; } = string.Empty;
        public string BackendStoreCountryCode { get; set; } = string.Empty;
        public string BackendPcId { get; set; } = string.Empty;
        public string BackendPcName { get; set; } = "pc_name_001";
        public string BackendSoftwareVersion { get; set; } = "1.1.1.0";
        public Dictionary<int, string> BackendDeviceObjectMappings { get; set; } = new();
        public List<BackendServerObjectMappingSource> BackendServerObjects { get; set; } = new();
        public int BackendSyncIntervalSeconds { get; set; } = 5;

        public void Save()
        {
            SaveAppSettings();

#if WINDOWS
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                localSettings.Values[KeyComPort] = LastComPort;
                localSettings.Values[KeyBaudRate] = LastBaudRate;
                localSettings.Values[KeyTheme] = Theme;
                localSettings.Values[KeyIsVirtualModeEnabled] = IsVirtualModeEnabled;
                localSettings.Values[KeyIsLastPortAutoConnectEnabled] = IsLastPortAutoConnectEnabled;
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
            _backendApiKeyStore.Save(BackendApiKey);
        }

        public void Load()
        {
            bool appSettingsLoaded = LoadAppSettings();

#if WINDOWS
            if (!appSettingsLoaded)
            {
                try
                {
                    var localSettings = ApplicationData.Current.LocalSettings;
                    if (localSettings.Values.TryGetValue(KeyComPort, out var port)) LastComPort = (string)port;
                    if (localSettings.Values.TryGetValue(KeyBaudRate, out var rate)) LastBaudRate = (int)rate;
                    if (localSettings.Values.TryGetValue(KeyTheme, out var theme)) Theme = (string)theme;
                    if (localSettings.Values.TryGetValue(KeyIsVirtualModeEnabled, out var isVirtual)) IsVirtualModeEnabled = (bool)isVirtual;
                    if (localSettings.Values.TryGetValue(KeyIsLastPortAutoConnectEnabled, out var autoConnect)) IsLastPortAutoConnectEnabled = (bool)autoConnect;
                    if (localSettings.Values.TryGetValue(KeyLanguage, out var lang)) Language = (string)lang;
                    if (localSettings.Values.TryGetValue(KeyResponseTimeout, out var timeout))
                    {
                        ResponseTimeoutSeconds = SettingValueConverter.ReadDouble(timeout, ResponseTimeoutSeconds);
                    }
                    if (localSettings.Values.TryGetValue(KeyIsPeriodicPingEnabled, out var pingEnabled)) IsPeriodicPingEnabled = (bool)pingEnabled;
                    if (localSettings.Values.TryGetValue(KeyPingIntervalSeconds, out var pingInterval))
                    {
                        PingIntervalSeconds = SettingValueConverter.ReadDouble(pingInterval, PingIntervalSeconds);
                    }
                    if (localSettings.Values.TryGetValue(KeyPingCountryCode, out var pingCountryCode)) PingCountryCode = (string)pingCountryCode;
                    if (localSettings.Values.TryGetValue(KeyPingUtcOffsetMinutes, out var pingOffset)) PingUtcOffsetMinutes = (int)pingOffset;
                }
                catch
                {
                }
            }
#endif

            LoadBackendSettings();
            BackendApiKey = _backendApiKeyStore.Load();
        }

        private void SaveAppSettings()
        {
            try
            {
                string filePath = GetAppSettingsFilePath();
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var settings = new AppSettingsFile(
                    LastComPort,
                    LastBaudRate,
                    Theme,
                    IsVirtualModeEnabled,
                    IsLastPortAutoConnectEnabled,
                    Language,
                    ResponseTimeoutSeconds,
                    IsPeriodicPingEnabled,
                    PingIntervalSeconds,
                    PingCountryCode,
                    PingUtcOffsetMinutes,
                    NormalizeScanStartId(ScanStartId, ScanEndId),
                    NormalizeScanEndId(ScanStartId, ScanEndId));

                string json = JsonSerializer.Serialize(settings, BackendJsonOptions);
                File.WriteAllText(filePath, json);
            }
            catch
            {
            }
        }

        private bool LoadAppSettings()
        {
            try
            {
                string filePath = GetAppSettingsFilePath();
                if (!File.Exists(filePath))
                {
                    return false;
                }

                string json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<AppSettingsFile>(json, BackendJsonOptions);
                if (settings is null)
                {
                    return false;
                }

                LastComPort = settings.LastComPort ?? LastComPort;
                LastBaudRate = settings.LastBaudRate == 0 ? LastBaudRate : settings.LastBaudRate;
                Theme = settings.Theme;
                IsVirtualModeEnabled = settings.IsVirtualModeEnabled;
                IsLastPortAutoConnectEnabled = settings.IsLastPortAutoConnectEnabled;
                Language = settings.Language ?? Language;
                ResponseTimeoutSeconds = settings.ResponseTimeoutSeconds == 0 ? ResponseTimeoutSeconds : settings.ResponseTimeoutSeconds;
                IsPeriodicPingEnabled = settings.IsPeriodicPingEnabled;
                PingIntervalSeconds = settings.PingIntervalSeconds < 0.1 ? PingIntervalSeconds : settings.PingIntervalSeconds;
                PingCountryCode = settings.PingCountryCode ?? PingCountryCode;
                PingUtcOffsetMinutes = settings.PingUtcOffsetMinutes;
                ScanStartId = NormalizeScanStartId(settings.ScanStartId, settings.ScanEndId);
                ScanEndId = NormalizeScanEndId(settings.ScanStartId, settings.ScanEndId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string GetAppSettingsFilePath()
        {
            string? directory = Path.GetDirectoryName(_backendSettingsPathProvider.BackendSettingsFilePath);
            return string.IsNullOrWhiteSpace(directory)
                ? AppSettingsFileName
                : Path.Combine(directory, AppSettingsFileName);
        }

        private static int NormalizeScanStartId(int startId, int endId)
            => Math.Min(ClampScanId(startId, 1), ClampScanId(endId, 10));

        private static int NormalizeScanEndId(int startId, int endId)
            => Math.Max(ClampScanId(startId, 1), ClampScanId(endId, 10));

        private static int ClampScanId(int value, int fallback)
        {
            int id = value == 0 ? fallback : value;
            return Math.Clamp(id, MinScanId, MaxScanId);
        }

        private static string NormalizeTheme(string? theme)
        {
            if (string.Equals(theme, ThemeLight, StringComparison.OrdinalIgnoreCase))
            {
                return ThemeLight;
            }

            if (string.Equals(theme, ThemeDark, StringComparison.OrdinalIgnoreCase))
            {
                return ThemeDark;
            }

            return ThemeDefault;
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
                    BackendServerObjects,
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
                BackendServerObjects = settings.BackendServerObjects ?? new List<BackendServerObjectMappingSource>();
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
            List<BackendServerObjectMappingSource>? BackendServerObjects,
            int BackendSyncIntervalSeconds);

        private sealed record AppSettingsFile(
            string LastComPort,
            int LastBaudRate,
            string Theme,
            bool IsVirtualModeEnabled,
            bool IsLastPortAutoConnectEnabled,
            string Language,
            double ResponseTimeoutSeconds,
            bool IsPeriodicPingEnabled,
            double PingIntervalSeconds,
            string PingCountryCode,
            int PingUtcOffsetMinutes,
            int ScanStartId,
            int ScanEndId);
    }
}
