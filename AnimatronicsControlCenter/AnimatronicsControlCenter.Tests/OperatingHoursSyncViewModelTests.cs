using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;
using AnimatronicsControlCenter.Infrastructure;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class OperatingHoursSyncViewModelTests
{
    [TestMethod]
    public void Constructor_InitializesDeviceRangeFromSettings()
    {
        var settings = TestSettings();
        settings.ScanStartId = 4;
        settings.ScanEndId = 6;

        var viewModel = new OperatingHoursSyncViewModel(
            settings,
            new FakeSource(),
            new FakeSyncService(),
            new FakeSerialService());

        Assert.AreEqual(4, viewModel.StartDeviceId);
        Assert.AreEqual(6, viewModel.EndDeviceId);
        Assert.AreEqual("UTC+09:00", viewModel.CurrentTimezoneOffsetText);
    }

    [TestMethod]
    public async Task LoadScheduleCommand_ShowsScheduleAndTimezoneWarningWithoutChangingSettings()
    {
        var settings = TestSettings();
        settings.PingUtcOffsetMinutes = 480;
        var schedule = TestSchedule(timezone: "Asia/Seoul");
        var viewModel = new OperatingHoursSyncViewModel(
            settings,
            new FakeSource { Result = new OperatingHoursSourceResult(true, false, "OK", schedule) },
            new FakeSyncService(),
            new FakeSerialService());

        await viewModel.LoadScheduleCommand.ExecuteAsync(null);

        Assert.AreSame(schedule, viewModel.Schedule);
        Assert.AreEqual(480, settings.PingUtcOffsetMinutes);
        StringAssert.Contains(viewModel.TimezoneWarningText, "Asia/Seoul");
    }

    [TestMethod]
    public async Task SyncCommand_WritesUserSelectedRange()
    {
        var schedule = TestSchedule();
        var sync = new FakeSyncService();
        var viewModel = new OperatingHoursSyncViewModel(
            TestSettings(),
            new FakeSource { Result = new OperatingHoursSourceResult(true, false, "OK", schedule) },
            sync,
            new FakeSerialService())
        {
            StartDeviceId = 2,
            EndDeviceId = 3
        };
        await viewModel.LoadScheduleCommand.ExecuteAsync(null);

        await viewModel.SyncCommand.ExecuteAsync(null);

        Assert.AreEqual(2, sync.StartDeviceId);
        Assert.AreEqual(3, sync.EndDeviceId);
        Assert.AreEqual(2, viewModel.DeviceResults.Count);
        Assert.AreEqual(OperatingHoursDeviceSyncStatus.Synced, viewModel.DeviceResults[0].WriteStatus);
    }

    [TestMethod]
    public async Task ReadAndCompareCommand_MarksMismatchWhenDeviceChecksumDiffers()
    {
        var schedule = TestSchedule();
        var serial = new FakeSerialService
        {
            ReadResult = new OperatingHoursDeviceReadResult(
                1,
                true,
                new OperatingHoursDeviceSchedule(540, schedule.Checksum + 1, schedule.Days),
                "OK")
        };
        var viewModel = new OperatingHoursSyncViewModel(
            TestSettings(),
            new FakeSource { Result = new OperatingHoursSourceResult(true, false, "OK", schedule) },
            new FakeSyncService(),
            serial)
        {
            StartDeviceId = 1,
            EndDeviceId = 1
        };
        await viewModel.LoadScheduleCommand.ExecuteAsync(null);

        await viewModel.ReadAndCompareCommand.ExecuteAsync(null);

        Assert.AreEqual(OperatingHoursDeviceSyncStatus.Mismatch, viewModel.DeviceResults[0].ReadStatus);
    }

    private static SettingsService TestSettings()
    {
        string path = Path.Combine(Path.GetTempPath(), "ui_winui3_operating_hours_vm_tests", Guid.NewGuid().ToString("N"), "backend-settings.json");
        return new SettingsService(new FakeBackendSettingsPathProvider(path));
    }

    private static OperatingHoursSchedule TestSchedule(string timezone = "Asia/Seoul")
    {
        var days = new[]
        {
            new OperatingHoursDay("MON", false, 540, 1080),
            new OperatingHoursDay("TUE", false, 540, 1080),
            new OperatingHoursDay("WED", false, 540, 1080),
            new OperatingHoursDay("THU", false, 540, 1080),
            new OperatingHoursDay("FRI", false, 540, 1080),
            new OperatingHoursDay("SAT", true, 0, 0),
            new OperatingHoursDay("SUN", true, 0, 0),
        };

        return new OperatingHoursSchedule("store-1", "Seoul Store", timezone, "2026-02-26T01:49:43.727Z", days, 1234);
    }

    private sealed class FakeSource : IOperatingHoursSource
    {
        public OperatingHoursSourceResult? Result { get; set; }
        public Task<OperatingHoursSourceResult> LoadAsync(CancellationToken cancellationToken)
            => Task.FromResult(Result ?? new OperatingHoursSourceResult(false, false, "No schedule", null));
    }

    private sealed class FakeSyncService : IOperatingHoursDeviceSyncService
    {
        public int StartDeviceId { get; private set; }
        public int EndDeviceId { get; private set; }

        public Task<IReadOnlyList<OperatingHoursDeviceWriteResult>> SyncRangeAsync(int startDeviceId, int endDeviceId, OperatingHoursSchedule schedule, CancellationToken cancellationToken)
        {
            StartDeviceId = startDeviceId;
            EndDeviceId = endDeviceId;
            return Task.FromResult<IReadOnlyList<OperatingHoursDeviceWriteResult>>(
                Enumerable.Range(startDeviceId, endDeviceId - startDeviceId + 1)
                    .Select(id => new OperatingHoursDeviceWriteResult(id, true, schedule.Checksum, "OK"))
                    .ToArray());
        }
    }

    private sealed class FakeSerialService : ISerialService
    {
        public OperatingHoursDeviceReadResult? ReadResult { get; set; }
        public bool IsConnected => true;
        public Task ConnectAsync(string portName, int baudRate) => Task.CompletedTask;
        public void Disconnect() { }
        public Task SendBinaryCommandAsync(int deviceId, byte[] packet) => Task.CompletedTask;
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, AnimatronicsControlCenter.Core.Protocol.BinaryCommand cmd) => Task.FromResult<byte[]?>(null);
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, AnimatronicsControlCenter.Core.Protocol.BinaryCommand cmd, byte[] packet) => Task.FromResult<byte[]?>(null);
        public Task<AnimatronicsControlCenter.Core.Models.Device?> PingDeviceAsync(int deviceId) => Task.FromResult<AnimatronicsControlCenter.Core.Models.Device?>(null);
        public Task<IEnumerable<AnimatronicsControlCenter.Core.Models.Device>> ScanDevicesAsync(int startId, int endId) => Task.FromResult(Enumerable.Empty<AnimatronicsControlCenter.Core.Models.Device>());
        public Task<OperatingHoursDeviceWriteResult> SetOperatingHoursAsync(int deviceId, OperatingHoursSchedule schedule)
            => Task.FromResult(new OperatingHoursDeviceWriteResult(deviceId, true, schedule.Checksum, "OK"));
        public Task<OperatingHoursDeviceReadResult> GetOperatingHoursAsync(int deviceId)
            => Task.FromResult(ReadResult ?? new OperatingHoursDeviceReadResult(deviceId, false, null, "No result"));
    }

    private sealed class FakeBackendSettingsPathProvider : IBackendSettingsPathProvider
    {
        public FakeBackendSettingsPathProvider(string filePath)
        {
            BackendSettingsFilePath = filePath;
        }

        public string BackendSettingsFilePath { get; }
    }
}
