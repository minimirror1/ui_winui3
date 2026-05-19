using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class OperatingHoursSourceTests
{
    [TestMethod]
    public async Task LoadAsync_FetchesSelectedStoreScheduleAndCachesIt()
    {
        var settings = TestSettings();
        settings.BackendStoreId = "store-1";
        var cache = new MemoryOperatingHoursCache();
        var catalog = new FakeCatalogClient { Result = Success(StoreDetail("store-1")) };
        var source = new OperatingHoursSource(settings, catalog, cache);

        var result = await source.LoadAsync(CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsFalse(result.FromCache);
        Assert.AreEqual("store-1", result.Schedule!.StoreId);
        Assert.AreSame(result.Schedule, cache.SavedSchedule);
    }

    [TestMethod]
    public async Task LoadAsync_BackendFailureReturnsCachedSchedule()
    {
        var settings = TestSettings();
        settings.BackendStoreId = "store-1";
        var cached = OperatingHoursSchedule.FromStoreDetail(StoreDetail("cached-store"));
        var cache = new MemoryOperatingHoursCache { LoadedSchedule = cached };
        var catalog = new FakeCatalogClient { Result = new BackendFetchResult<BackendStoreDetailResponse>(false, 500, "Server error", null) };
        var source = new OperatingHoursSource(settings, catalog, cache);

        var result = await source.LoadAsync(CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.FromCache);
        Assert.AreSame(cached, result.Schedule);
    }

    [TestMethod]
    public async Task LoadAsync_BackendFailureWithoutCacheReturnsFailureMessage()
    {
        var settings = TestSettings();
        settings.BackendStoreId = "store-1";
        var source = new OperatingHoursSource(
            settings,
            new FakeCatalogClient { Result = new BackendFetchResult<BackendStoreDetailResponse>(false, 404, "Not found", null) },
            new MemoryOperatingHoursCache());

        var result = await source.LoadAsync(CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.IsNull(result.Schedule);
        StringAssert.Contains(result.Message, "Not found");
    }

    private static SettingsService TestSettings()
    {
        string path = Path.Combine(Path.GetTempPath(), "ui_winui3_operating_hours_source_tests", Guid.NewGuid().ToString("N"), "backend-settings.json");
        return new SettingsService(new FakeBackendSettingsPathProvider(path));
    }

    private static BackendFetchResult<BackendStoreDetailResponse> Success(BackendStoreDetailResponse detail)
        => new(true, 200, "OK", detail);

    private static BackendStoreDetailResponse StoreDetail(string storeId)
        => new(
            StoreId: storeId,
            StoreName: "Seoul Store",
            CountryCode: "KR",
            Pcs: Array.Empty<BackendPcDetailResponse>(),
            Timezone: "Asia/Seoul",
            OperateTimes: new[]
            {
                new BackendStoreOperateTime("MON", "09:00", "18:00"),
                new BackendStoreOperateTime("TUE", "09:00", "18:00"),
                new BackendStoreOperateTime("WED", "09:00", "18:00"),
                new BackendStoreOperateTime("THU", "09:00", "18:00"),
                new BackendStoreOperateTime("FRI", "09:00", "18:00"),
                new BackendStoreOperateTime("SAT", "00:00", "00:00"),
                new BackendStoreOperateTime("SUN", "00:00", "00:00"),
            },
            ModifiedAt: "2026-02-26T01:49:43.727Z");

    private sealed class MemoryOperatingHoursCache : IOperatingHoursCache
    {
        public OperatingHoursSchedule? LoadedSchedule { get; set; }
        public OperatingHoursSchedule? SavedSchedule { get; private set; }

        public Task SaveAsync(OperatingHoursSchedule schedule, CancellationToken cancellationToken)
        {
            SavedSchedule = schedule;
            return Task.CompletedTask;
        }

        public Task<OperatingHoursSchedule?> LoadAsync(CancellationToken cancellationToken)
            => Task.FromResult(LoadedSchedule);
    }

    private sealed class FakeBackendSettingsPathProvider : IBackendSettingsPathProvider
    {
        public FakeBackendSettingsPathProvider(string filePath)
        {
            BackendSettingsFilePath = filePath;
        }

        public string BackendSettingsFilePath { get; }
    }

    private sealed class FakeCatalogClient : IBackendServerCatalogClient
    {
        public BackendFetchResult<BackendStoreDetailResponse>? Result { get; set; }

        public Task<BackendFetchResult<BackendStoreDetailResponse>> GetStoreDetailAsync(string storeId, CancellationToken cancellationToken)
            => Task.FromResult(Result ?? new BackendFetchResult<BackendStoreDetailResponse>(false, null, "No result", null));

        public Task<BackendFetchResult<BackendStoreListResponse>> GetStoreListAsync(string countryCode, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<BackendSendResult> UpdatePcMetadataAsync(string storeId, string pcId, BackendPcUpdateRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<BackendFetchResult<BackendStoreCreateResponse>> CreateStoreAsync(BackendStoreCreateRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<BackendSendResult> UpdateStoreAsync(string storeId, BackendStoreUpdateRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<BackendFetchResult<BackendPcAddResponse>> CreatePcAsync(string storeId, BackendPcCreateRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<BackendFetchResult<BackendObjectCreateResponse>> CreateObjectAsync(string storeId, string pcId, BackendObjectCreateRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<BackendSendResult> UpdateObjectAsync(string objectId, BackendObjectUpdateRequest request, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }
}
