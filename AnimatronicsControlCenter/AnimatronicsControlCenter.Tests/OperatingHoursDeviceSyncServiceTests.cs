using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class OperatingHoursDeviceSyncServiceTests
{
    [TestMethod]
    public async Task SyncRangeAsync_WritesInclusiveRangeAndContinuesAfterFailure()
    {
        var serial = new FakeSerialService { FailDeviceId = 2 };
        var sync = new OperatingHoursDeviceSyncService(serial, TestSettings());
        var schedule = TestSchedule();

        var results = await sync.SyncRangeAsync(1, 3, schedule, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, serial.WrittenDeviceIds);
        Assert.AreEqual(3, results.Count);
        Assert.IsTrue(results.Single(result => result.DeviceId == 1).Success);
        Assert.IsFalse(results.Single(result => result.DeviceId == 2).Success);
        Assert.IsTrue(results.Single(result => result.DeviceId == 3).Success);
    }

    private static SettingsService TestSettings()
    {
        string path = Path.Combine(Path.GetTempPath(), "ui_winui3_operating_hours_sync_tests", Guid.NewGuid().ToString("N"), "backend-settings.json");
        var settings = new SettingsService(new FakeBackendSettingsPathProvider(path));
        settings.PingUtcOffsetMinutes = 540;
        return settings;
    }

    private static OperatingHoursSchedule TestSchedule()
    {
        var days = new[]
        {
            new OperatingHoursDay("MON", 540, 1080),
            new OperatingHoursDay("TUE", 540, 1080),
            new OperatingHoursDay("WED", 540, 1080),
            new OperatingHoursDay("THU", 540, 1080),
            new OperatingHoursDay("FRI", 540, 1080),
            new OperatingHoursDay("SAT", 0, 0),
            new OperatingHoursDay("SUN", 0, 0),
        };

        return new OperatingHoursSchedule("store-1", "Seoul Store", "Asia/Seoul", null, days, 1234);
    }

    private sealed class FakeSerialService : ISerialService
    {
        public int FailDeviceId { get; set; }
        public List<int> WrittenDeviceIds { get; } = new();
        public bool IsConnected => true;
        public Task ConnectAsync(string portName, int baudRate) => Task.CompletedTask;
        public void Disconnect() { }
        public Task SendBinaryCommandAsync(int deviceId, byte[] packet) => Task.CompletedTask;
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, AnimatronicsControlCenter.Core.Protocol.BinaryCommand cmd) => Task.FromResult<byte[]?>(null);
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, AnimatronicsControlCenter.Core.Protocol.BinaryCommand cmd, byte[] packet) => Task.FromResult<byte[]?>(null);
        public Task<AnimatronicsControlCenter.Core.Models.Device?> PingDeviceAsync(int deviceId) => Task.FromResult<AnimatronicsControlCenter.Core.Models.Device?>(null);
        public Task<IEnumerable<AnimatronicsControlCenter.Core.Models.Device>> ScanDevicesAsync(int startId, int endId) => Task.FromResult(Enumerable.Empty<AnimatronicsControlCenter.Core.Models.Device>());

        public Task<OperatingHoursDeviceWriteResult> SetOperatingHoursAsync(int deviceId, OperatingHoursSchedule schedule)
        {
            WrittenDeviceIds.Add(deviceId);
            bool success = deviceId != FailDeviceId;
            return Task.FromResult(new OperatingHoursDeviceWriteResult(deviceId, success, success ? schedule.Checksum : 0, success ? "OK" : "Failed"));
        }

        public Task<OperatingHoursDeviceReadResult> GetOperatingHoursAsync(int deviceId)
            => Task.FromResult(new OperatingHoursDeviceReadResult(deviceId, false, null, "Not implemented"));
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
