using System.Globalization;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class ScanDialogViewModelTests
{
    [TestMethod]
    public void Constructor_UsesSettingsScanRangeAsInitialValues()
    {
        var settings = new FakeSettingsService
        {
            ScanStartId = 4,
            ScanEndId = 14
        };

        var viewModel = new ScanDialogViewModel(
            new FakeSerialService(),
            new FakeLocalizationService(),
            settings);

        Assert.AreEqual(4, viewModel.StartId);
        Assert.AreEqual(14, viewModel.EndId);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public string LastComPort { get; set; } = "COM1";
        public int LastBaudRate { get; set; } = 115200;
        public string Theme { get; set; } = "Default";
        public bool IsVirtualModeEnabled { get; set; }
        public bool IsLastPortAutoConnectEnabled { get; set; }
        public string Language { get; set; } = "ko-KR";
        public double ResponseTimeoutSeconds { get; set; } = 2.0;
        public bool IsPeriodicPingEnabled { get; set; } = true;
        public double PingIntervalSeconds { get; set; } = 5;
        public string PingCountryCode { get; set; } = "KR";
        public int PingUtcOffsetMinutes { get; set; } = 540;
        public int ScanStartId { get; set; } = 1;
        public int ScanEndId { get; set; } = 10;
        public string AppSettingsFilePath { get; } = string.Empty;
        public bool IsBackendSyncEnabled { get; set; } = true;
        public string BackendBaseUrl { get; set; } = string.Empty;
        public string BackendBearerToken { get; set; } = string.Empty;
        public string BackendStoreId { get; set; } = string.Empty;
        public string BackendStoreName { get; set; } = string.Empty;
        public string BackendStoreCountryCode { get; set; } = string.Empty;
        public string BackendPcId { get; set; } = string.Empty;
        public string BackendPcName { get; set; } = string.Empty;
        public string BackendSoftwareVersion { get; set; } = string.Empty;
        public Dictionary<int, string> BackendDeviceObjectMappings { get; set; } = new();
        public List<BackendServerObjectMappingSource> BackendServerObjects { get; set; } = new();
        public int BackendSyncIntervalSeconds { get; set; } = 5;
        public void Save() { }
        public void Load() { }
    }

    private sealed class FakeLocalizationService : ILocalizationService
    {
        public event EventHandler? LanguageChanged;
        public CultureInfo CurrentCulture { get; set; } = CultureInfo.GetCultureInfo("ko-KR");
        public string GetString(string key) => key;
        public void SetLanguage(string languageCode)
        {
            CurrentCulture = CultureInfo.GetCultureInfo(languageCode);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class FakeSerialService : ISerialService
    {
        public bool IsConnected => false;
        public Task ConnectAsync(string portName, int baudRate) => Task.CompletedTask;
        public void Disconnect() { }
        public Task SendBinaryCommandAsync(int deviceId, byte[] packet) => Task.CompletedTask;
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, AnimatronicsControlCenter.Core.Protocol.BinaryCommand cmd)
            => Task.FromResult<byte[]?>(null);
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, AnimatronicsControlCenter.Core.Protocol.BinaryCommand cmd, byte[] packet)
            => Task.FromResult<byte[]?>(null);
        public Task<Device?> PingDeviceAsync(int deviceId) => Task.FromResult<Device?>(null);
        public Task<IEnumerable<Device>> ScanDevicesAsync(int startId, int endId)
            => Task.FromResult<IEnumerable<Device>>(Array.Empty<Device>());
    }
}
