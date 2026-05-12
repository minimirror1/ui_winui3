using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnimatronicsControlCenter.UI.ViewModels;

public sealed partial class OperateTimeEntry : ObservableObject
{
    [ObservableProperty] private string dayOfWeek = string.Empty;
    [ObservableProperty] private string openTime = "09:00";
    [ObservableProperty] private string closeTime = "18:00";

    public OperateTimeEntry(string day) => DayOfWeek = day;
}

public sealed record RegistrationResult(
    string StoreId,
    string? StoreName,
    string? CountryCode,
    string PcId,
    string? PcName,
    string ObjectId,
    string? ObjectName);

public partial class BackendRegistrationViewModel : ObservableObject
{
    private readonly IBackendServerCatalogClient _catalogClient;

    // ── 내비게이션 ──────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepTitle))]
    [NotifyPropertyChangedFor(nameof(IsStep1Visible))]
    [NotifyPropertyChangedFor(nameof(IsStep2Visible))]
    [NotifyPropertyChangedFor(nameof(IsStep3Visible))]
    [NotifyPropertyChangedFor(nameof(IsBackVisible))]
    [NotifyPropertyChangedFor(nameof(IsNextVisible))]
    [NotifyPropertyChangedFor(nameof(IsFinishVisible))]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private int currentStep = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewMode))]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private bool isSelectMode = true;

    [ObservableProperty] private bool isEditExpanded = false;
    [ObservableProperty] private bool isBusy = false;
    [ObservableProperty] private string statusMessage = string.Empty;

    public bool IsNewMode
    {
        get => !IsSelectMode;
        set => IsSelectMode = !value;
    }
    public string StepTitle => CurrentStep switch
    {
        1 => "단계 1/3 — Store",
        2 => "단계 2/3 — PC",
        _ => "단계 3/3 — Object"
    };
    public bool IsStep1Visible => CurrentStep == 1;
    public bool IsStep2Visible => CurrentStep == 2;
    public bool IsStep3Visible => CurrentStep == 3;
    public bool IsBackVisible => CurrentStep > 1;
    public bool IsNextVisible => CurrentStep < 3;
    public bool IsFinishVisible => CurrentStep == 3;

    // ── Step 1: Store ─────────────────────────────────
    public ObservableCollection<BackendStoreSummaryResponse> AvailableStores { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStoreSelected))]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private BackendStoreSummaryResponse? selectedStore;

    public bool IsStoreSelected => SelectedStore is not null;

    // 기존 선택 수정 폼
    [ObservableProperty] private string editStoreName = string.Empty;
    [ObservableProperty] private string editStoreCountryCode = string.Empty;
    [ObservableProperty] private string editStoreAddress = string.Empty;
    [ObservableProperty] private string editStoreLatitude = string.Empty;
    [ObservableProperty] private string editStoreLongitude = string.Empty;
    [ObservableProperty] private string editStoreTimezone = string.Empty;

    // 신규 등록 폼
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newStoreName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newStoreCountryCode = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newStoreAddress = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newStoreLatitude = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newStoreLongitude = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newStoreTimezone = string.Empty;

    public ObservableCollection<OperateTimeEntry> NewStoreOperateTimes { get; }
    public IReadOnlyList<string> OperationStatusOptions { get; } = ["PLAY", "STOP", "REPEAT"];

    // ── Step 2: PC ────────────────────────────────────
    public ObservableCollection<BackendPcDetailResponse> AvailablePcs { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPcSelected))]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private BackendPcDetailResponse? selectedPc;

    public bool IsPcSelected => SelectedPc is not null;

    [ObservableProperty] private string editPcName = string.Empty;
    [ObservableProperty] private string editPcSwVersion = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newPcName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newPcSwVersion = string.Empty;

    // ── Step 3: Object ────────────────────────────────
    public ObservableCollection<BackendObjectDetailResponse> AvailableObjects { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsObjectSelected))]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private BackendObjectDetailResponse? selectedObject;

    public bool IsObjectSelected => SelectedObject is not null;

    [ObservableProperty] private string editObjectName = string.Empty;
    [ObservableProperty] private string editObjectStartTime = string.Empty;
    [ObservableProperty] private string editObjectEndTime = string.Empty;
    [ObservableProperty] private bool editObjectScheduleFlag = false;
    [ObservableProperty] private string editObjectOperationStatus = string.Empty;
    [ObservableProperty] private string editObjectFirmwareBoardId = string.Empty;
    [ObservableProperty] private string editObjectFirmwareBoardType = string.Empty;
    [ObservableProperty] private string editObjectFirmwareVersion = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newObjectName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newObjectStartTime = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newObjectEndTime = string.Empty;

    [ObservableProperty] private bool newObjectScheduleFlag = false;
    [ObservableProperty] private string newObjectOperationStatus = "PLAY";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newObjectFirmwareBoardId = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newObjectFirmwareBoardType = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GoNextCommand))]
    private string newObjectFirmwareVersion = string.Empty;

    // ── 결과 + 누적 ID ────────────────────────────────
    public RegistrationResult? Result { get; private set; }
    public event Action? RequestClose;

    private string? _resolvedStoreId;
    private string? _resolvedStoreName;
    private string? _resolvedCountryCode;
    private string? _resolvedPcId;
    private string? _resolvedPcName;
    private StoreEditValues? _originalStoreEditValues;

    public BackendRegistrationViewModel(
        IBackendServerCatalogClient catalogClient,
        IReadOnlyList<BackendStoreSummaryResponse> availableStores)
    {
        _catalogClient = catalogClient;
        foreach (var store in availableStores)
            AvailableStores.Add(store);

        NewStoreOperateTimes = new ObservableCollection<OperateTimeEntry>(
            new[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" }
                .Select(d => new OperateTimeEntry(d)));
    }

    private bool CanGoNext() => CurrentStep switch
    {
        1 when IsSelectMode => SelectedStore is not null,
        1 => !string.IsNullOrWhiteSpace(NewStoreName) &&
             !string.IsNullOrWhiteSpace(NewStoreCountryCode) &&
             !string.IsNullOrWhiteSpace(NewStoreAddress) &&
             double.TryParse(NewStoreLatitude, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
             double.TryParse(NewStoreLongitude, NumberStyles.Float, CultureInfo.InvariantCulture, out _) &&
             !string.IsNullOrWhiteSpace(NewStoreTimezone),
        2 when IsSelectMode => SelectedPc is not null,
        2 => !string.IsNullOrWhiteSpace(NewPcName) && !string.IsNullOrWhiteSpace(NewPcSwVersion),
        3 when IsSelectMode => SelectedObject is not null,
        3 => !string.IsNullOrWhiteSpace(NewObjectName) &&
             !string.IsNullOrWhiteSpace(NewObjectStartTime) &&
             !string.IsNullOrWhiteSpace(NewObjectEndTime) &&
             !string.IsNullOrWhiteSpace(NewObjectFirmwareBoardId) &&
             !string.IsNullOrWhiteSpace(NewObjectFirmwareBoardType) &&
             !string.IsNullOrWhiteSpace(NewObjectFirmwareVersion),
        _ => false
    };

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task GoNextAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            bool ok = CurrentStep switch
            {
                1 => await ProcessStep1Async(),
                2 => await ProcessStep2Async(),
                3 => await ProcessStep3Async(),
                _ => false
            };
            if (!ok) return;

            if (CurrentStep < 3)
            {
                CurrentStep++;
                IsSelectMode = true;
                IsEditExpanded = false;
            }
            else
            {
                RequestClose?.Invoke();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
            IsSelectMode = true;
            IsEditExpanded = false;
        }
    }

    [RelayCommand]
    private async Task ToggleEditAsync()
    {
        IsEditExpanded = !IsEditExpanded;
        if (IsEditExpanded && CurrentStep == 1 && IsSelectMode && SelectedStore is not null)
            await PopulateStoreEditFieldsAsync(SelectedStore);
    }

    private async Task PopulateStoreEditFieldsAsync(BackendStoreSummaryResponse store)
    {
        EditStoreName = store.StoreName ?? string.Empty;
        EditStoreCountryCode = store.CountryCode ?? string.Empty;
        EditStoreAddress = string.Empty;
        EditStoreLatitude = string.Empty;
        EditStoreLongitude = string.Empty;
        EditStoreTimezone = string.Empty;

        var detail = await _catalogClient.GetStoreDetailAsync(store.StoreId, CancellationToken.None);
        if (!detail.Success || detail.Data is null)
        {
            StatusMessage = detail.Message;
            CaptureOriginalStoreEditValues();
            return;
        }

        EditStoreName = detail.Data.StoreName ?? EditStoreName;
        EditStoreCountryCode = detail.Data.CountryCode ?? EditStoreCountryCode;
        EditStoreAddress = detail.Data.Address ?? string.Empty;
        EditStoreLatitude = FormatNullableDouble(detail.Data.Latitude);
        EditStoreLongitude = FormatNullableDouble(detail.Data.Longitude);
        EditStoreTimezone = detail.Data.Timezone ?? string.Empty;
        CaptureOriginalStoreEditValues();
    }

    private async Task<bool> ProcessStep1Async()
    {
        if (IsSelectMode)
        {
            _resolvedStoreId = SelectedStore!.StoreId;
            _resolvedStoreName = SelectedStore.StoreName;
            _resolvedCountryCode = SelectedStore.CountryCode;

            if (IsEditExpanded && HasAnyStoreEdit())
            {
                double? lat = double.TryParse(EditStoreLatitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double latVal) ? latVal : null;
                double? lng = double.TryParse(EditStoreLongitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double lngVal) ? lngVal : null;
                var updateResult = await _catalogClient.UpdateStoreAsync(
                    _resolvedStoreId,
                    new BackendStoreUpdateRequest(
                        NullIfEmpty(EditStoreName), NullIfEmpty(EditStoreCountryCode),
                        NullIfEmpty(EditStoreAddress), lat, lng,
                        NullIfEmpty(EditStoreTimezone), null),
                    CancellationToken.None);
                if (!updateResult.Success)
                {
                    StatusMessage = updateResult.Message;
                    return false;
                }
            }

            var detail = await _catalogClient.GetStoreDetailAsync(_resolvedStoreId, CancellationToken.None);
            if (!detail.Success || detail.Data is null)
            {
                StatusMessage = detail.Message;
                return false;
            }
            AvailablePcs.Clear();
            foreach (var pc in detail.Data.Pcs)
                AvailablePcs.Add(pc);
        }
        else
        {
            var operateTimes = NewStoreOperateTimes
                .Select(e => new BackendStoreOperateTime(e.DayOfWeek, e.OpenTime, e.CloseTime))
                .ToArray();
            var createResult = await _catalogClient.CreateStoreAsync(
                new BackendStoreCreateRequest(
                    NewStoreName, NewStoreCountryCode, NewStoreAddress,
                    double.Parse(NewStoreLatitude, CultureInfo.InvariantCulture), double.Parse(NewStoreLongitude, CultureInfo.InvariantCulture),
                    NewStoreTimezone, operateTimes),
                CancellationToken.None);
            if (!createResult.Success || createResult.Data is null)
            {
                StatusMessage = createResult.Message;
                return false;
            }
            _resolvedStoreId = createResult.Data.Id;
            _resolvedStoreName = createResult.Data.StoreName;
            _resolvedCountryCode = NewStoreCountryCode;
            AvailablePcs.Clear();
        }
        return true;
    }

    private async Task<bool> ProcessStep2Async()
    {
        if (IsSelectMode)
        {
            _resolvedPcId = SelectedPc!.PcId;
            _resolvedPcName = SelectedPc.PcName;

            if (IsEditExpanded && HasAnyPcEdit())
            {
                var updateResult = await _catalogClient.UpdatePcMetadataAsync(
                    _resolvedStoreId!,
                    _resolvedPcId,
                    new BackendPcUpdateRequest(
                        string.IsNullOrWhiteSpace(EditPcName) ? SelectedPc.PcName ?? string.Empty : EditPcName,
                        string.IsNullOrWhiteSpace(EditPcSwVersion) ? SelectedPc.SwVersion ?? string.Empty : EditPcSwVersion),
                    CancellationToken.None);
                if (!updateResult.Success)
                {
                    StatusMessage = updateResult.Message;
                    return false;
                }
            }

            AvailableObjects.Clear();
            foreach (var obj in SelectedPc.Objects)
                AvailableObjects.Add(obj);
        }
        else
        {
            var createResult = await _catalogClient.CreatePcAsync(
                _resolvedStoreId!,
                new BackendPcCreateRequest(NewPcName, NewPcSwVersion),
                CancellationToken.None);
            if (!createResult.Success || createResult.Data is null)
            {
                StatusMessage = createResult.Message;
                return false;
            }
            _resolvedPcId = createResult.Data.PcId;
            _resolvedPcName = NewPcName;
            AvailableObjects.Clear();
        }
        return true;
    }

    private async Task<bool> ProcessStep3Async()
    {
        string objectId;
        string? objectName;

        if (IsSelectMode)
        {
            objectId = SelectedObject!.Id;
            objectName = SelectedObject.ObjectName;

            if (IsEditExpanded && HasAnyObjectEdit())
            {
                BackendTimeRange? timeRange = !string.IsNullOrWhiteSpace(EditObjectStartTime) && !string.IsNullOrWhiteSpace(EditObjectEndTime)
                    ? new BackendTimeRange(EditObjectStartTime, EditObjectEndTime)
                    : null;
                BackendFirmwareVersion? firmware = !string.IsNullOrWhiteSpace(EditObjectFirmwareBoardId)
                    ? new BackendFirmwareVersion(EditObjectFirmwareBoardId, EditObjectFirmwareBoardType, EditObjectFirmwareVersion)
                    : null;
                var updateResult = await _catalogClient.UpdateObjectAsync(
                    objectId,
                    new BackendObjectUpdateRequest(
                        NullIfEmpty(EditObjectName), timeRange, null,
                        firmware, NullIfEmpty(EditObjectOperationStatus)),
                    CancellationToken.None);
                if (!updateResult.Success)
                {
                    StatusMessage = updateResult.Message;
                    return false;
                }
            }
        }
        else
        {
            var createResult = await _catalogClient.CreateObjectAsync(
                _resolvedStoreId!,
                _resolvedPcId!,
                new BackendObjectCreateRequest(
                    NewObjectName,
                    new BackendTimeRange(NewObjectStartTime, NewObjectEndTime),
                    NewObjectScheduleFlag,
                    new BackendFirmwareVersion(NewObjectFirmwareBoardId, NewObjectFirmwareBoardType, NewObjectFirmwareVersion),
                    NewObjectOperationStatus),
                CancellationToken.None);
            if (!createResult.Success || createResult.Data is null)
            {
                StatusMessage = createResult.Message;
                return false;
            }
            objectId = createResult.Data.ObjectId;
            objectName = NewObjectName;
        }

        Result = new RegistrationResult(
            _resolvedStoreId!, _resolvedStoreName, _resolvedCountryCode,
            _resolvedPcId!, _resolvedPcName,
            objectId, objectName);
        return true;
    }

    private bool HasAnyStoreEdit() =>
        CaptureCurrentStoreEditValues() != (_originalStoreEditValues ?? StoreEditValues.Empty);

    private bool HasAnyPcEdit() =>
        !string.IsNullOrWhiteSpace(EditPcName) || !string.IsNullOrWhiteSpace(EditPcSwVersion);

    private bool HasAnyObjectEdit() =>
        !string.IsNullOrWhiteSpace(EditObjectName) ||
        !string.IsNullOrWhiteSpace(EditObjectStartTime) ||
        !string.IsNullOrWhiteSpace(EditObjectEndTime) ||
        !string.IsNullOrWhiteSpace(EditObjectFirmwareBoardId) ||
        !string.IsNullOrWhiteSpace(EditObjectFirmwareBoardType) ||
        !string.IsNullOrWhiteSpace(EditObjectFirmwareVersion) ||
        !string.IsNullOrWhiteSpace(EditObjectOperationStatus);

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    private void CaptureOriginalStoreEditValues() =>
        _originalStoreEditValues = CaptureCurrentStoreEditValues();

    private StoreEditValues CaptureCurrentStoreEditValues() =>
        new(EditStoreName, EditStoreCountryCode, EditStoreAddress, EditStoreLatitude, EditStoreLongitude, EditStoreTimezone);

    private static string FormatNullableDouble(double? value) =>
        value?.ToString("G", CultureInfo.InvariantCulture) ?? string.Empty;

    private sealed record StoreEditValues(
        string StoreName,
        string CountryCode,
        string Address,
        string Latitude,
        string Longitude,
        string Timezone)
    {
        public static StoreEditValues Empty { get; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }
}
