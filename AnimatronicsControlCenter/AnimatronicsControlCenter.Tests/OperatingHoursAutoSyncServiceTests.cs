using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class OperatingHoursAutoSyncServiceTests
{
    [TestMethod]
    public async Task RunOnceAsync_LoadsScheduleAndSyncsConfiguredRange()
    {
        var settings = TestSettings();
        settings.ScanStartId = 2;
        settings.ScanEndId = 4;
        var schedule = TestSchedule();
        var sync = new FakeSyncService();
        var service = new OperatingHoursAutoSyncService(
            settings,
            new FakeSource { Result = new OperatingHoursSourceResult(true, false, "OK", schedule) },
            sync,
            () => new DateTimeOffset(2026, 5, 19, 16, 12, 0, TimeSpan.Zero));

        await service.RunOnceAsync(CancellationToken.None);

        Assert.AreEqual(2, sync.StartDeviceId);
        Assert.AreEqual(4, sync.EndDeviceId);
        Assert.AreSame(schedule, sync.Schedule);
    }

    [TestMethod]
    public void GetDelayToNextTopOfHour_ReturnsRemainingTimeToNextHour()
    {
        var delay = OperatingHoursAutoSyncService.GetDelayToNextTopOfHour(
            new DateTimeOffset(2026, 5, 19, 16, 12, 30, TimeSpan.Zero));

        Assert.AreEqual(TimeSpan.FromMinutes(47.5), delay);
    }

    private static SettingsService TestSettings()
    {
        string path = Path.Combine(Path.GetTempPath(), "ui_winui3_operating_hours_auto_tests", Guid.NewGuid().ToString("N"), "backend-settings.json");
        return new SettingsService(new FakeBackendSettingsPathProvider(path));
    }

    private static OperatingHoursSchedule TestSchedule()
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

        return new OperatingHoursSchedule("store-1", "Seoul Store", "Asia/Seoul", null, days, 1234);
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
        public OperatingHoursSchedule? Schedule { get; private set; }

        public Task<IReadOnlyList<OperatingHoursDeviceWriteResult>> SyncRangeAsync(int startDeviceId, int endDeviceId, OperatingHoursSchedule schedule, CancellationToken cancellationToken)
        {
            StartDeviceId = startDeviceId;
            EndDeviceId = endDeviceId;
            Schedule = schedule;
            return Task.FromResult<IReadOnlyList<OperatingHoursDeviceWriteResult>>(Array.Empty<OperatingHoursDeviceWriteResult>());
        }
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
