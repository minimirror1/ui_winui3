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
        void Save();
        void Load();
    }
}

