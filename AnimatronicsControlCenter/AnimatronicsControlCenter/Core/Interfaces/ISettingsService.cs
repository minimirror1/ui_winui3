namespace AnimatronicsControlCenter.Core.Interfaces
{
    public interface ISettingsService
    {
        string LastComPort { get; set; }
        int LastBaudRate { get; set; }
        string Theme { get; set; }
        bool IsVirtualModeEnabled { get; set; }
        void Save();
        void Load();
    }
}

