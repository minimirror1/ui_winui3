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
    public void SelectedServerPc_PopulatesServerObjectsAndSnapshot()
    {
        var catalog = new FakeCatalogClient
        {
            StoreDetail = StoreDetail("pc-1", "Main PC", "1.1.1.0", "obj-1")
        };
        var viewModel = new BackendSettingsViewModel(TestSettings(), catalog);
        viewModel.SelectedServerStore = new BackendStoreSummaryResponse("store-1", "Seoul Store", "KR");

        viewModel.SelectedServerPc = viewModel.ServerPcList[0];

        Assert.AreEqual(1, viewModel.ServerObjects.Count);
        Assert.AreEqual("obj-1", viewModel.ServerObjects[0].ObjectId);
    }

    [TestMethod]
    public void ApplyServerValues_AfterPcSelection_SetsLocalSettingsFields()
    {
        var catalog = new FakeCatalogClient
        {
            StoreDetail = StoreDetail("pc-1", "Main PC", "1.1.1.0", "obj-1")
        };
        var viewModel = new BackendSettingsViewModel(TestSettings(), catalog);
        viewModel.SelectedServerStore = new BackendStoreSummaryResponse("store-1", "Seoul Store", "KR");
        viewModel.SelectedServerPc = viewModel.ServerPcList[0];

        viewModel.ApplyServerValuesCommand.Execute(null);

        Assert.AreEqual("store-1", viewModel.BackendStoreId);
        Assert.AreEqual("Seoul Store", viewModel.BackendStoreName);
        Assert.AreEqual("KR", viewModel.BackendStoreCountryCode);
        Assert.AreEqual("pc-1", viewModel.BackendPcId);
        Assert.AreEqual("Main PC", viewModel.BackendPcName);
        Assert.AreEqual("1.1.1.0", viewModel.BackendSoftwareVersion);
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
    public void DataManagement_IsAvailableOnlyAfterCountryCodeSelection()
    {
        var viewModel = new BackendSettingsViewModel(TestSettings(), new FakeCatalogClient());

        Assert.IsFalse(viewModel.IsRegistrationAvailable);
        Assert.IsTrue(viewModel.ShouldShowRegistrationCountryCodeHint);

        viewModel.SelectedCountryCode = "KR";
        Assert.IsTrue(viewModel.IsRegistrationAvailable);
        Assert.IsFalse(viewModel.ShouldShowRegistrationCountryCodeHint);

        viewModel.SelectedCountryCode = null;
        Assert.IsFalse(viewModel.IsRegistrationAvailable);
        Assert.IsTrue(viewModel.ShouldShowRegistrationCountryCodeHint);
    }

    [TestMethod]
    public void ServerConnectionFields_AreLockedByDefaultAndToggleEditable()
    {
        var viewModel = new BackendSettingsViewModel(TestSettings(), new FakeCatalogClient());

        Assert.IsFalse(viewModel.IsServerConnectionEditing);
        Assert.AreEqual("서버 접속 정보 수정", viewModel.ServerConnectionEditButtonText);

        viewModel.ToggleServerConnectionEditingCommand.Execute(null);

        Assert.IsTrue(viewModel.IsServerConnectionEditing);
        Assert.AreEqual("서버 접속 정보 잠금", viewModel.ServerConnectionEditButtonText);

        viewModel.ToggleServerConnectionEditingCommand.Execute(null);

        Assert.IsFalse(viewModel.IsServerConnectionEditing);
        Assert.AreEqual("서버 접속 정보 수정", viewModel.ServerConnectionEditButtonText);
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

    [TestMethod]
    public void SelectedServerStore_PopulatesServerPcList()
    {
        var catalog = new FakeCatalogClient
        {
            StoreDetail = StoreDetail("pc-1", "Main PC", "1.1.1.0", "obj-1")
        };
        var viewModel = new BackendSettingsViewModel(TestSettings(), catalog);

        viewModel.SelectedServerStore = new BackendStoreSummaryResponse("store-1", "Seoul Store", "KR");

        Assert.AreEqual(1, viewModel.ServerPcList.Count);
        Assert.AreEqual("Main PC", viewModel.ServerPcList[0].PcName);
    }

    [TestMethod]
    public void SelectedServerStore_Changed_ClearsPcAndObjects()
    {
        var catalog = new FakeCatalogClient
        {
            StoreDetail = StoreDetail("pc-1", "Main PC", "1.1.1.0", "obj-1")
        };
        var viewModel = new BackendSettingsViewModel(TestSettings(), catalog);
        viewModel.SelectedServerStore = new BackendStoreSummaryResponse("store-1", "Seoul Store", "KR");
        viewModel.SelectedServerPc = viewModel.ServerPcList[0];

        catalog.StoreDetail = null;
        viewModel.SelectedServerStore = new BackendStoreSummaryResponse("store-2", "Tokyo Store", "JP");

        Assert.AreEqual(0, viewModel.ServerPcList.Count);
        Assert.IsNull(viewModel.SelectedServerPc);
        Assert.AreEqual(0, viewModel.ServerObjects.Count);
    }

    [TestMethod]
    public void SelectedServerStore_ErrorResponse_ShowsStatusMessage()
    {
        var catalog = new FakeCatalogClient { StoreDetail = null };
        var viewModel = new BackendSettingsViewModel(TestSettings(), catalog);

        viewModel.SelectedServerStore = new BackendStoreSummaryResponse("bad-id", "Bad Store", "KR");

        Assert.AreEqual(0, viewModel.ServerPcList.Count);
        Assert.IsFalse(string.IsNullOrEmpty(viewModel.ServerStatusMessage));
    }

    [TestMethod]
    public async Task HandleRegistrationResultAsync_RefreshesStoreListAndAutoSelects()
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

        var result = new RegistrationResult("store-1", "Seoul Store", "KR", "pc-1", "Main PC", "obj-1", "Robot A");
        await viewModel.HandleRegistrationResultAsync(result);

        Assert.AreEqual(1, viewModel.ServerStoreList.Count);
        Assert.IsNotNull(viewModel.SelectedServerStore);
        Assert.AreEqual("store-1", viewModel.SelectedServerStore!.StoreId);
        Assert.IsNotNull(viewModel.SelectedServerPc);
        Assert.AreEqual("pc-1", viewModel.SelectedServerPc!.PcId);
        Assert.AreEqual(1, catalog.GetStoreDetailCallCount);
        Assert.AreEqual(1, viewModel.ServerPcList.Count);
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

        public int GetStoreDetailCallCount { get; private set; }

        public Task<BackendFetchResult<BackendStoreDetailResponse>> GetStoreDetailAsync(string storeId, CancellationToken cancellationToken)
        {
            GetStoreDetailCallCount++;
            bool ok = StoreDetail is not null;
            return Task.FromResult(new BackendFetchResult<BackendStoreDetailResponse>(
                ok, ok ? 200 : 404, ok ? "OK" : "Not found.", StoreDetail));
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

        public Task<BackendFetchResult<BackendStoreCreateResponse>> CreateStoreAsync(
            BackendStoreCreateRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BackendFetchResult<BackendStoreCreateResponse>(
                true, 201, "OK", new BackendStoreCreateResponse("new-store-id", request.StoreName, "2026-01-01")));

        public Task<BackendSendResult> UpdateStoreAsync(
            string storeId, BackendStoreUpdateRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BackendSendResult(true, 200, "OK"));

        public Task<BackendFetchResult<BackendPcAddResponse>> CreatePcAsync(
            string storeId, BackendPcCreateRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BackendFetchResult<BackendPcAddResponse>(
                true, 201, "OK", new BackendPcAddResponse(storeId, "new-pc-id", "2026-01-01")));

        public Task<BackendFetchResult<BackendObjectCreateResponse>> CreateObjectAsync(
            string storeId, string pcId, BackendObjectCreateRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BackendFetchResult<BackendObjectCreateResponse>(
                true, 201, "OK", new BackendObjectCreateResponse("new-obj-id", "2026-01-01")));

        public Task<BackendSendResult> UpdateObjectAsync(
            string objectId, BackendObjectUpdateRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BackendSendResult(true, 200, "OK"));
    }
}
