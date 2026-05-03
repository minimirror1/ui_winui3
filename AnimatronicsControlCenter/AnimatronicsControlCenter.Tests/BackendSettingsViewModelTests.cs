using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Infrastructure;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendSettingsViewModelTests
{
    [TestMethod]
    public async Task FetchServerCommand_SelectsCurrentPcSnapshot()
    {
        var settings = TestSettings();
        settings.BackendStoreId = "store-1";
        settings.BackendPcId = "pc-1";
        var catalog = new FakeCatalogClient
        {
            StoreDetail = StoreDetail("pc-1", "Main PC", "1.1.1.0", "obj-1")
        };
        var viewModel = new BackendSettingsViewModel(settings, catalog);

        await viewModel.FetchServerCommand.ExecuteAsync(null);

        Assert.AreEqual("store-1", viewModel.ServerStoreId);
        Assert.AreEqual("pc-1", viewModel.ServerPcId);
        Assert.AreEqual("obj-1", viewModel.ServerObjects[0].ObjectId);
    }

    [TestMethod]
    public async Task SaveCommand_ParsesMappingsSavesSettingsAndReportsPcMetadata()
    {
        var settings = TestSettings();
        var catalog = new FakeCatalogClient();
        var viewModel = new BackendSettingsViewModel(settings, catalog)
        {
            BackendStoreId = "store-1",
            BackendPcId = "pc-1",
            BackendPcName = "Main PC",
            BackendSoftwareVersion = "1.2.3.4",
            BackendDeviceObjectMappingsText = """{"2":"obj-1"}"""
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.AreEqual("obj-1", settings.BackendDeviceObjectMappings[2]);
        Assert.AreEqual("store-1", catalog.UpdatedStoreId);
        Assert.AreEqual("pc-1", catalog.UpdatedPcId);
        Assert.AreEqual("Main PC", catalog.UpdatedRequest!.PcName);
        Assert.AreEqual("1.2.3.4", catalog.UpdatedRequest.SwVersion);
    }

    [TestMethod]
    public async Task SaveCommand_InvalidMappingJson_DoesNotOverwriteMappings()
    {
        var settings = TestSettings();
        settings.BackendDeviceObjectMappings = new Dictionary<int, string> { [2] = "obj-1" };
        var viewModel = new BackendSettingsViewModel(settings, new FakeCatalogClient())
        {
            BackendDeviceObjectMappingsText = "{bad json"
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.AreEqual("obj-1", settings.BackendDeviceObjectMappings[2]);
        StringAssert.Contains(viewModel.BackendDeviceObjectMappingsMessage, "JSON");
    }

    [TestMethod]
    public void SelectedCountryCode_PopulatesServerStoreList()
    {
        var catalog = new FakeCatalogClient
        {
            StoreList = new BackendStoreListResponse(new[]
            {
                new BackendStoreSummaryResponse("store-1", "Seoul Store", "KR")
            })
        };
        var viewModel = new BackendSettingsViewModel(TestSettings(), catalog);

        viewModel.SelectedCountryCode = "KR";

        Assert.AreEqual(1, viewModel.ServerStoreList.Count);
        Assert.AreEqual("Seoul Store", viewModel.ServerStoreList[0].StoreName);
    }

    [TestMethod]
    public void SelectedCountryCode_Changed_ClearsStoreAndPcSelections()
    {
        var catalog = new FakeCatalogClient
        {
            StoreList = new BackendStoreListResponse(new[]
            {
                new BackendStoreSummaryResponse("store-1", "Seoul Store", "KR")
            }),
            StoreDetail = StoreDetail("pc-1", "Main PC", "1.0", "obj-1")
        };
        var viewModel = new BackendSettingsViewModel(TestSettings(), catalog);
        viewModel.SelectedCountryCode = "KR";
        viewModel.SelectedServerStore = viewModel.ServerStoreList[0];

        catalog.StoreList = null; // JP에는 스토어 없음
        viewModel.SelectedCountryCode = "JP";

        Assert.AreEqual(0, viewModel.ServerStoreList.Count);
        Assert.IsNull(viewModel.SelectedServerStore);
    }

    private static SettingsService TestSettings()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ui_winui3_backend_settings_vm_tests", Guid.NewGuid().ToString("N"), "backend-settings.json");
        return new SettingsService(new FakeBackendSettingsPathProvider(path));
    }

    private static BackendStoreDetailResponse StoreDetail(string pcId, string pcName, string swVersion, string objectId)
    {
        return new BackendStoreDetailResponse(
            StoreId: "store-1",
            StoreName: "Seoul Store",
            CountryCode: "KR",
            Pcs: new[]
            {
                new BackendPcDetailResponse(
                    PcId: pcId,
                    PcName: pcName,
                    SwVersion: swVersion,
                    Objects: new[] { new BackendObjectDetailResponse(objectId, "Robot A", "ON", Array.Empty<BackendErrorData>()) })
            });
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
        public BackendStoreDetailResponse? StoreDetail { get; set; }
        public BackendStoreListResponse? StoreList { get; set; }
        public string? UpdatedStoreId { get; private set; }
        public string? UpdatedPcId { get; private set; }
        public BackendPcUpdateRequest? UpdatedRequest { get; private set; }

        public Task<BackendFetchResult<BackendStoreDetailResponse>> GetStoreDetailAsync(string storeId, CancellationToken cancellationToken)
        {
            bool success = StoreDetail is not null;
            string message = success ? "OK" : "Store not found.";
            return Task.FromResult(new BackendFetchResult<BackendStoreDetailResponse>(success, success ? 200 : 404, message, StoreDetail));
        }

        public Task<BackendFetchResult<BackendStoreListResponse>> GetStoreListAsync(string countryCode, CancellationToken cancellationToken)
        {
            return Task.FromResult(new BackendFetchResult<BackendStoreListResponse>(
                StoreList is not null, 200, "OK", StoreList));
        }

        public Task<BackendSendResult> UpdatePcMetadataAsync(string storeId, string pcId, BackendPcUpdateRequest request, CancellationToken cancellationToken)
        {
            UpdatedStoreId = storeId;
            UpdatedPcId = pcId;
            UpdatedRequest = request;
            return Task.FromResult(new BackendSendResult(true, 200, "OK"));
        }
    }
}
