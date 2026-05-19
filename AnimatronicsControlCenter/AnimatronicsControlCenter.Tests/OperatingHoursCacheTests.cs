using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class OperatingHoursCacheTests
{
    [TestMethod]
    public async Task SaveAsync_WritesOperatingHoursCacheNextToBackendSettings()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ui_winui3_operating_hours_cache_tests", Guid.NewGuid().ToString("N"));
        var cache = new OperatingHoursCache(new FakeBackendSettingsPathProvider(Path.Combine(directory, "backend-settings.json")));
        var schedule = TestSchedule();

        await cache.SaveAsync(schedule, CancellationToken.None);

        Assert.IsTrue(File.Exists(Path.Combine(directory, "operating-hours-cache.json")));
    }

    [TestMethod]
    public async Task LoadAsync_ReturnsSavedSchedule()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ui_winui3_operating_hours_cache_tests", Guid.NewGuid().ToString("N"));
        var cache = new OperatingHoursCache(new FakeBackendSettingsPathProvider(Path.Combine(directory, "backend-settings.json")));
        var schedule = TestSchedule();

        await cache.SaveAsync(schedule, CancellationToken.None);
        var loaded = await cache.LoadAsync(CancellationToken.None);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(schedule.StoreId, loaded.StoreId);
        Assert.AreEqual(schedule.Checksum, loaded.Checksum);
        Assert.AreEqual(7, loaded.Days.Count);
    }

    [TestMethod]
    public async Task LoadAsync_MissingOrCorruptFileReturnsNull()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ui_winui3_operating_hours_cache_tests", Guid.NewGuid().ToString("N"));
        var cache = new OperatingHoursCache(new FakeBackendSettingsPathProvider(Path.Combine(directory, "backend-settings.json")));

        Assert.IsNull(await cache.LoadAsync(CancellationToken.None));

        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "operating-hours-cache.json"), "{bad json");

        Assert.IsNull(await cache.LoadAsync(CancellationToken.None));
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

        return new OperatingHoursSchedule(
            "store-1",
            "Seoul Store",
            "Asia/Seoul",
            "2026-02-26T01:49:43.727Z",
            days,
            OperatingHoursSchedule.ComputeChecksum("Asia/Seoul", "2026-02-26T01:49:43.727Z", days));
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
