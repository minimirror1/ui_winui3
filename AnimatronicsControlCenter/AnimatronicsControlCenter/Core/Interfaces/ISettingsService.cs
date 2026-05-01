using System.Collections.Generic;

namespace AnimatronicsControlCenter.Core.Interfaces
{
    public interface ISettingsService
    {
        string LastComPort { get; set; }
        int LastBaudRate { get; set; }
        string Theme { get; set; }
        bool IsVirtualModeEnabled { get; set; }
        string Language { get; set; }
        double ResponseTimeoutSeconds { get; set; }
        bool IsPeriodicPingEnabled { get; set; }
        int PingIntervalSeconds { get; set; }
        string PingCountryCode { get; set; }
        int PingUtcOffsetMinutes { get; set; }
        bool IsBackendSyncEnabled { get; set; }
        string BackendBaseUrl { get; set; }
        string BackendBearerToken { get; set; }
        string BackendStoreId { get; set; }
        string BackendStoreName { get; set; }
        string BackendStoreCountryCode { get; set; }
        string BackendPcId { get; set; }
        string BackendPcName { get; set; }
        string BackendSoftwareVersion { get; set; }
        Dictionary<int, string> BackendDeviceObjectMappings { get; set; }
        int BackendSyncIntervalSeconds { get; set; }
        void Save();
        void Load();
    }
}
