using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendRegistrationViewModelTests
{
    // ── 초기 상태 ──────────────────────────────────────────────

    [TestMethod]
    public void Constructor_Step1_SelectMode_WithAvailableStores()
    {
        var vm = MakeVm(stores: [new BackendStoreSummaryResponse("s1", "Seoul", "KR")]);

        Assert.AreEqual(1, vm.CurrentStep);
        Assert.IsTrue(vm.IsSelectMode);
        Assert.AreEqual(1, vm.AvailableStores.Count);
    }

    [TestMethod]
    public void Constructor_OperateTimes_Has7Rows()
    {
        var vm = MakeVm();
        Assert.AreEqual(7, vm.NewStoreOperateTimes.Count);
        Assert.AreEqual("MON", vm.NewStoreOperateTimes[0].DayOfWeek);
        Assert.AreEqual("SUN", vm.NewStoreOperateTimes[6].DayOfWeek);
    }

    // ── CanGoNext 유효성 ──────────────────────────────────────

    [TestMethod]
    public void GoNextCommand_Step1_SelectMode_DisabledWhenNoStoreSelected()
    {
        var vm = MakeVm();
        vm.IsSelectMode = true;
        vm.SelectedStore = null;

        Assert.IsFalse(vm.GoNextCommand.CanExecute(null));
    }

    [TestMethod]
    public void GoNextCommand_Step1_SelectMode_EnabledWhenStoreSelected()
    {
        var vm = MakeVm();
        vm.IsSelectMode = true;
        vm.SelectedStore = new BackendStoreSummaryResponse("s1", "Seoul", "KR");

        Assert.IsTrue(vm.GoNextCommand.CanExecute(null));
    }

    [TestMethod]
    public void GoNextCommand_Step1_NewMode_DisabledWhenFieldsMissing()
    {
        var vm = MakeVm();
        vm.IsSelectMode = false;
        vm.NewStoreName = "Test";
        // CountryCode 미입력 → disabled

        Assert.IsFalse(vm.GoNextCommand.CanExecute(null));
    }

    [TestMethod]
    public void GoNextCommand_Step1_NewMode_EnabledWhenAllRequiredFieldsFilled()
    {
        var vm = MakeVm();
        vm.IsSelectMode = false;
        FillStep1NewForm(vm);

        Assert.IsTrue(vm.GoNextCommand.CanExecute(null));
    }

    // ── Step 1 → 2: 기존 선택 ────────────────────────────────

    [TestMethod]
    public async Task GoNext_Step1_ExistingStore_AdvancesToStep2_LoadsPcs()
    {
        var catalog = new FakeRegCatalogClient
        {
            StoreDetail = MakeStoreDetail("s1", "pc-1", "Main PC", "1.0", "obj-1")
        };
        var vm = MakeVm(catalog: catalog,
            stores: [new BackendStoreSummaryResponse("s1", "Seoul", "KR")]);
        vm.IsSelectMode = true;
        vm.SelectedStore = vm.AvailableStores[0];

        await vm.GoNextCommand.ExecuteAsync(null);

        Assert.AreEqual(2, vm.CurrentStep);
        Assert.AreEqual(1, vm.AvailablePcs.Count);
        Assert.AreEqual("Main PC", vm.AvailablePcs[0].PcName);
        Assert.IsFalse(catalog.CreateStoreCalled);
    }

    // ── Step 1 → 2: 신규 등록 ────────────────────────────────

    [TestMethod]
    public async Task GoNext_Step1_NewStore_CallsCreateStoreAndAdvancesToStep2()
    {
        var catalog = new FakeRegCatalogClient();
        var vm = MakeVm(catalog: catalog);
        vm.IsSelectMode = false;
        FillStep1NewForm(vm);

        await vm.GoNextCommand.ExecuteAsync(null);

        Assert.IsTrue(catalog.CreateStoreCalled);
        Assert.AreEqual("Seoul Store", catalog.CreatedStoreName);
        Assert.AreEqual(2, vm.CurrentStep);
        Assert.AreEqual(0, vm.AvailablePcs.Count); // 새 스토어는 PC 없음
    }

    [TestMethod]
    public async Task GoNext_Step1_NewStore_ApiFailure_StaysOnStep1WithMessage()
    {
        var catalog = new FakeRegCatalogClient { ShouldCreateStoreFail = true };
        var vm = MakeVm(catalog: catalog);
        vm.IsSelectMode = false;
        FillStep1NewForm(vm);

        await vm.GoNextCommand.ExecuteAsync(null);

        Assert.AreEqual(1, vm.CurrentStep);
        Assert.IsFalse(string.IsNullOrEmpty(vm.StatusMessage));
        Assert.IsFalse(vm.IsBusy);
    }

    // ── Step 2: 이전으로 ──────────────────────────────────────

    [TestMethod]
    public async Task GoBack_FromStep2_ReturnToStep1()
    {
        var catalog = new FakeRegCatalogClient
        {
            StoreDetail = MakeStoreDetail("s1", "pc-1", "PC", "1.0", "obj-1")
        };
        var vm = MakeVm(catalog: catalog,
            stores: [new BackendStoreSummaryResponse("s1", "Seoul", "KR")]);
        vm.SelectedStore = vm.AvailableStores[0];
        await vm.GoNextCommand.ExecuteAsync(null); // step 1 → 2

        vm.GoBackCommand.Execute(null);

        Assert.AreEqual(1, vm.CurrentStep);
        Assert.IsTrue(vm.IsSelectMode);
    }

    // ── Step 2 → 3: 신규 PC ──────────────────────────────────

    [TestMethod]
    public async Task GoNext_Step2_NewPc_CallsCreatePcWithResolvedStoreId()
    {
        var catalog = new FakeRegCatalogClient();
        var vm = MakeVm(catalog: catalog);
        vm.IsSelectMode = false;
        FillStep1NewForm(vm);
        await vm.GoNextCommand.ExecuteAsync(null); // step 1 → 2

        vm.IsSelectMode = false;
        vm.NewPcName = "New PC";
        vm.NewPcSwVersion = "2.0";
        await vm.GoNextCommand.ExecuteAsync(null); // step 2 → 3

        Assert.IsTrue(catalog.CreatePcCalled);
        Assert.AreEqual("new-store-id", catalog.CreatePcStoreId);
        Assert.AreEqual(3, vm.CurrentStep);
    }

    // ── Step 3 → 완료: 신규 Object ───────────────────────────

    [TestMethod]
    public async Task GoNext_Step3_NewObject_CallsCreateObjectAndSetsResult()
    {
        var catalog = new FakeRegCatalogClient();
        var vm = MakeVm(catalog: catalog);

        // Step 1: 신규 Store
        vm.IsSelectMode = false;
        FillStep1NewForm(vm);
        await vm.GoNextCommand.ExecuteAsync(null);

        // Step 2: 신규 PC
        vm.IsSelectMode = false;
        vm.NewPcName = "New PC";
        vm.NewPcSwVersion = "2.0";
        await vm.GoNextCommand.ExecuteAsync(null);

        // Step 3: 신규 Object
        vm.IsSelectMode = false;
        FillStep3NewForm(vm);
        bool closeFired = false;
        vm.RequestClose += () => closeFired = true;
        await vm.GoNextCommand.ExecuteAsync(null);

        Assert.IsTrue(catalog.CreateObjectCalled);
        Assert.IsNotNull(vm.Result);
        Assert.AreEqual("new-store-id", vm.Result.StoreId);
        Assert.AreEqual("new-pc-id", vm.Result.PcId);
        Assert.AreEqual("new-obj-id", vm.Result.ObjectId);
        Assert.AreEqual("KR", vm.Result.CountryCode);
        Assert.IsTrue(closeFired);
    }

    // ── Step 3 → 완료: 기존 Object 선택 ─────────────────────

    [TestMethod]
    public async Task GoNext_Step3_ExistingObject_SetsResultFromSelectedObject()
    {
        var catalog = new FakeRegCatalogClient
        {
            StoreDetail = MakeStoreDetail("s1", "pc-1", "PC", "1.0", "obj-1")
        };
        var vm = MakeVm(catalog: catalog,
            stores: [new BackendStoreSummaryResponse("s1", "Seoul", "KR")]);

        // Step 1: 기존 선택
        vm.SelectedStore = vm.AvailableStores[0];
        await vm.GoNextCommand.ExecuteAsync(null);

        // Step 2: 기존 PC 선택
        vm.SelectedPc = vm.AvailablePcs[0];
        await vm.GoNextCommand.ExecuteAsync(null);

        // Step 3: 기존 Object 선택
        vm.SelectedObject = vm.AvailableObjects[0];
        await vm.GoNextCommand.ExecuteAsync(null);

        Assert.IsNotNull(vm.Result);
        Assert.AreEqual("s1", vm.Result.StoreId);
        Assert.AreEqual("pc-1", vm.Result.PcId);
        Assert.AreEqual("obj-1", vm.Result.ObjectId);
    }

    // ── 헬퍼 ──────────────────────────────────────────────────

    private static BackendRegistrationViewModel MakeVm(
        FakeRegCatalogClient? catalog = null,
        IReadOnlyList<BackendStoreSummaryResponse>? stores = null)
        => new(catalog ?? new FakeRegCatalogClient(), stores ?? Array.Empty<BackendStoreSummaryResponse>());

    private static void FillStep1NewForm(BackendRegistrationViewModel vm)
    {
        vm.NewStoreName = "Seoul Store";
        vm.NewStoreCountryCode = "KR";
        vm.NewStoreAddress = "123 Seoul St";
        vm.NewStoreLatitude = "37.5";
        vm.NewStoreLongitude = "127.0";
        vm.NewStoreTimezone = "Asia/Seoul";
    }

    private static void FillStep3NewForm(BackendRegistrationViewModel vm)
    {
        vm.NewObjectName = "Robot A";
        vm.NewObjectStartTime = "09:00";
        vm.NewObjectEndTime = "18:00";
        vm.NewObjectFirmwareBoardId = "B001";
        vm.NewObjectFirmwareBoardType = "Main";
        vm.NewObjectFirmwareVersion = "1.0.0";
    }

    private static BackendStoreDetailResponse MakeStoreDetail(
        string storeId, string pcId, string pcName, string swVersion, string objectId)
        => new(storeId, "Seoul Store", "KR",
            [new BackendPcDetailResponse(
                pcId, pcName, swVersion,
                [new BackendObjectDetailResponse(objectId, "Robot A", "ON", [])])]);

    private sealed class FakeRegCatalogClient : IBackendServerCatalogClient
    {
        public BackendStoreDetailResponse? StoreDetail { get; set; }
        public bool ShouldCreateStoreFail { get; set; }
        public bool CreateStoreCalled { get; private set; }
        public string? CreatedStoreName { get; private set; }
        public bool CreatePcCalled { get; private set; }
        public string? CreatePcStoreId { get; private set; }
        public bool CreateObjectCalled { get; private set; }

        public Task<BackendFetchResult<BackendStoreDetailResponse>> GetStoreDetailAsync(
            string storeId, CancellationToken cancellationToken)
        {
            bool ok = StoreDetail is not null;
            return Task.FromResult(new BackendFetchResult<BackendStoreDetailResponse>(
                ok, ok ? 200 : 404, ok ? "OK" : "Not found.", StoreDetail));
        }

        public Task<BackendFetchResult<BackendStoreListResponse>> GetStoreListAsync(
            string countryCode, CancellationToken cancellationToken)
            => Task.FromResult(new BackendFetchResult<BackendStoreListResponse>(true, 200, "OK",
                new BackendStoreListResponse([])));

        public Task<BackendSendResult> UpdatePcMetadataAsync(
            string storeId, string pcId, BackendPcUpdateRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BackendSendResult(true, 200, "OK"));

        public Task<BackendFetchResult<BackendStoreCreateResponse>> CreateStoreAsync(
            BackendStoreCreateRequest request, CancellationToken cancellationToken)
        {
            CreateStoreCalled = true;
            CreatedStoreName = request.StoreName;
            if (ShouldCreateStoreFail)
                return Task.FromResult(new BackendFetchResult<BackendStoreCreateResponse>(
                    false, 500, "Server error", null));
            return Task.FromResult(new BackendFetchResult<BackendStoreCreateResponse>(
                true, 201, "OK", new BackendStoreCreateResponse("new-store-id", request.StoreName, "2026-01-01")));
        }

        public Task<BackendSendResult> UpdateStoreAsync(
            string storeId, BackendStoreUpdateRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BackendSendResult(true, 200, "OK"));

        public Task<BackendFetchResult<BackendPcAddResponse>> CreatePcAsync(
            string storeId, BackendPcCreateRequest request, CancellationToken cancellationToken)
        {
            CreatePcCalled = true;
            CreatePcStoreId = storeId;
            return Task.FromResult(new BackendFetchResult<BackendPcAddResponse>(
                true, 201, "OK", new BackendPcAddResponse(storeId, "new-pc-id", "2026-01-01")));
        }

        public Task<BackendFetchResult<BackendObjectCreateResponse>> CreateObjectAsync(
            string storeId, string pcId, BackendObjectCreateRequest request, CancellationToken cancellationToken)
        {
            CreateObjectCalled = true;
            return Task.FromResult(new BackendFetchResult<BackendObjectCreateResponse>(
                true, 201, "OK", new BackendObjectCreateResponse("new-obj-id", "2026-01-01")));
        }

        public Task<BackendSendResult> UpdateObjectAsync(
            string objectId, BackendObjectUpdateRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new BackendSendResult(true, 200, "OK"));
    }
}
