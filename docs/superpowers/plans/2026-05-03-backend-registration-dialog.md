# Backend Registration Dialog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** BackendSettingsPage 왼쪽 패널에 "데이터 관리" 버튼을 추가하고, 클릭 시 Store → PC → Object 3단계 Wizard ContentDialog를 열어 등록/수정 후 드롭다운을 갱신하고 자동 선택한다.

**Architecture:** `BackendRegistrationViewModel`이 3단계 상태와 API 호출 로직을 담당하고, `BackendRegistrationDialog`(ContentDialog XAML + 코드비하인드)가 UI를 제공한다. 다이얼로그는 코드비하인드에서 직접 생성/표시하며, 완료 후 `BackendSettingsViewModel.HandleRegistrationResultAsync`가 드롭다운 갱신과 자동 선택을 처리한다.

**Tech Stack:** WinUI3, CommunityToolkit.Mvvm 8.2, MSTest, HttpClient, System.Text.Json

---

## 파일 구조

| 파일 | 역할 | 상태 |
|------|------|------|
| `Core/Backend/BackendDtos.cs` | 5개 DTO 레코드 추가 | 수정 |
| `Core/Interfaces/IBackendServerCatalogClient.cs` | 5개 메서드 추가 | 수정 |
| `Infrastructure/BackendServerCatalogClient.cs` | 5개 메서드 구현 | 수정 |
| `UI/ViewModels/BackendRegistrationViewModel.cs` | Wizard 상태·API 호출·결과 | 신규 |
| `UI/Views/BackendRegistrationDialog.xaml` | 3단계 Wizard ContentDialog UI | 신규 |
| `UI/Views/BackendRegistrationDialog.xaml.cs` | 코드비하인드·XamlRoot 설정 | 신규 |
| `UI/Views/BackendSettingsPage.xaml` | "데이터 관리" 버튼 추가 | 수정 |
| `UI/Views/BackendSettingsPage.xaml.cs` | 다이얼로그 열기 핸들러 | 수정 |
| `UI/ViewModels/BackendSettingsViewModel.cs` | HandleRegistrationResultAsync 추가 | 수정 |
| `AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj` | BackendRegistrationViewModel.cs 링크 추가 | 수정 |
| `AnimatronicsControlCenter.Tests/BackendRegistrationViewModelTests.cs` | 신규 테스트 | 신규 |
| `AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs` | FakeCatalogClient 확장 + HandleRegistrationResult 테스트 추가 | 수정 |
| `AnimatronicsControlCenter.Tests/BackendSettingsPageXamlTests.cs` | "데이터 관리" 버튼 확인 추가 | 수정 |

---

### Task 1: 신규 DTO 5개 추가

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Backend/BackendDtos.cs`

- [ ] **Step 1: BackendDtos.cs 파일 끝에 DTO 5개 추가**

파일 끝 (`BackendStoreSummaryResponse` 레코드 다음)에 아래를 추가:

```csharp
// PUT /v1/service/stores/{store_id}
public sealed record BackendStoreUpdateRequest(
    [property: JsonPropertyName("store_name")] string? StoreName,
    [property: JsonPropertyName("country_code")] string? CountryCode,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("latitude")] double? Latitude,
    [property: JsonPropertyName("longitude")] double? Longitude,
    [property: JsonPropertyName("timezone")] string? Timezone,
    [property: JsonPropertyName("operate_times")] IReadOnlyList<BackendStoreOperateTime>? OperateTimes);

// PUT /v1/service/objects/{object_id}
public sealed record BackendObjectUpdateRequest(
    [property: JsonPropertyName("object_name")] string? ObjectName,
    [property: JsonPropertyName("object_operation_time")] BackendTimeRange? ObjectOperationTime,
    [property: JsonPropertyName("schedule_flag")] bool? ScheduleFlag,
    [property: JsonPropertyName("firmware_version")] BackendFirmwareVersion? FirmwareVersion,
    [property: JsonPropertyName("operation_status")] string? OperationStatus);

// POST /v1/service/stores 응답
public sealed record BackendStoreCreateResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("store_name")] string StoreName,
    [property: JsonPropertyName("created_at")] string CreatedAt);

// POST /v1/service/stores/{store_id}/pcs 응답
public sealed record BackendPcAddResponse(
    [property: JsonPropertyName("store_id")] string StoreId,
    [property: JsonPropertyName("pc_id")] string PcId,
    [property: JsonPropertyName("created_at")] string CreatedAt);

// POST /v1/service/stores/{store_id}/pcs/{pc_id}/objects 응답
public sealed record BackendObjectCreateResponse(
    [property: JsonPropertyName("object_id")] string ObjectId,
    [property: JsonPropertyName("created_at")] string CreatedAt);
```

- [ ] **Step 2: 빌드로 문법 확인**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter/AnimatronicsControlCenter.csproj -p:Platform=x64
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Backend/BackendDtos.cs
git commit -m "feat: add registration dialog DTOs"
```

---

### Task 2: IBackendServerCatalogClient 확장 + BackendServerCatalogClient 구현

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IBackendServerCatalogClient.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendServerCatalogClient.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs` (FakeCatalogClient 확장)

- [ ] **Step 1: IBackendServerCatalogClient에 5개 메서드 추가**

`IBackendServerCatalogClient` 인터페이스 내부, `UpdatePcMetadataAsync` 아래에 추가:

```csharp
Task<BackendFetchResult<BackendStoreCreateResponse>> CreateStoreAsync(
    BackendStoreCreateRequest request,
    CancellationToken cancellationToken);

Task<BackendSendResult> UpdateStoreAsync(
    string storeId,
    BackendStoreUpdateRequest request,
    CancellationToken cancellationToken);

Task<BackendFetchResult<BackendPcAddResponse>> CreatePcAsync(
    string storeId,
    BackendPcCreateRequest request,
    CancellationToken cancellationToken);

Task<BackendFetchResult<BackendObjectCreateResponse>> CreateObjectAsync(
    string storeId,
    string pcId,
    BackendObjectCreateRequest request,
    CancellationToken cancellationToken);

Task<BackendSendResult> UpdateObjectAsync(
    string objectId,
    BackendObjectUpdateRequest request,
    CancellationToken cancellationToken);
```

- [ ] **Step 2: BackendServerCatalogClient에 5개 구현 추가**

`BackendServerCatalogClient.cs`의 `UpdatePcMetadataAsync` 메서드 다음에 추가. `GetStoreDetailAsync`와 동일한 try-catch 패턴 사용:

```csharp
public async Task<BackendFetchResult<BackendStoreCreateResponse>> CreateStoreAsync(
    BackendStoreCreateRequest request,
    CancellationToken cancellationToken)
{
    if (!BackendHttpRequest.TryCreateUri(_settingsService, "/v1/service/stores", out Uri uri, out string message))
        return new BackendFetchResult<BackendStoreCreateResponse>(false, null, message, null);

    try
    {
        using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Post, uri);
        httpRequest.Content = BackendHttpRequest.JsonContent(request);
        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new BackendFetchResult<BackendStoreCreateResponse>(false, (int)response.StatusCode, body, null);
        if (string.IsNullOrWhiteSpace(body))
            return new BackendFetchResult<BackendStoreCreateResponse>(false, (int)response.StatusCode, "Empty response.", null);
        var data = JsonSerializer.Deserialize<BackendStoreCreateResponse>(body, BackendHttpRequest.JsonOptions);
        return data is null
            ? new BackendFetchResult<BackendStoreCreateResponse>(false, (int)response.StatusCode, "Invalid response.", null)
            : new BackendFetchResult<BackendStoreCreateResponse>(true, (int)response.StatusCode, "OK", data);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
    {
        return new BackendFetchResult<BackendStoreCreateResponse>(false, null, ex.Message, null);
    }
}

public async Task<BackendSendResult> UpdateStoreAsync(
    string storeId,
    BackendStoreUpdateRequest request,
    CancellationToken cancellationToken)
{
    string path = $"/v1/service/stores/{Uri.EscapeDataString(storeId)}";
    if (!BackendHttpRequest.TryCreateUri(_settingsService, path, out Uri uri, out string message))
        return new BackendSendResult(false, null, message);

    try
    {
        using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Put, uri);
        httpRequest.Content = BackendHttpRequest.JsonContent(request);
        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new BackendSendResult(true, (int)response.StatusCode, "OK")
            : new BackendSendResult(false, (int)response.StatusCode, body);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        return new BackendSendResult(false, null, ex.Message);
    }
}

public async Task<BackendFetchResult<BackendPcAddResponse>> CreatePcAsync(
    string storeId,
    BackendPcCreateRequest request,
    CancellationToken cancellationToken)
{
    string path = $"/v1/service/stores/{Uri.EscapeDataString(storeId)}/pcs";
    if (!BackendHttpRequest.TryCreateUri(_settingsService, path, out Uri uri, out string message))
        return new BackendFetchResult<BackendPcAddResponse>(false, null, message, null);

    try
    {
        using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Post, uri);
        httpRequest.Content = BackendHttpRequest.JsonContent(request);
        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new BackendFetchResult<BackendPcAddResponse>(false, (int)response.StatusCode, body, null);
        if (string.IsNullOrWhiteSpace(body))
            return new BackendFetchResult<BackendPcAddResponse>(false, (int)response.StatusCode, "Empty response.", null);
        var data = JsonSerializer.Deserialize<BackendPcAddResponse>(body, BackendHttpRequest.JsonOptions);
        return data is null
            ? new BackendFetchResult<BackendPcAddResponse>(false, (int)response.StatusCode, "Invalid response.", null)
            : new BackendFetchResult<BackendPcAddResponse>(true, (int)response.StatusCode, "OK", data);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
    {
        return new BackendFetchResult<BackendPcAddResponse>(false, null, ex.Message, null);
    }
}

public async Task<BackendFetchResult<BackendObjectCreateResponse>> CreateObjectAsync(
    string storeId,
    string pcId,
    BackendObjectCreateRequest request,
    CancellationToken cancellationToken)
{
    string path = $"/v1/service/stores/{Uri.EscapeDataString(storeId)}/pcs/{Uri.EscapeDataString(pcId)}/objects";
    if (!BackendHttpRequest.TryCreateUri(_settingsService, path, out Uri uri, out string message))
        return new BackendFetchResult<BackendObjectCreateResponse>(false, null, message, null);

    try
    {
        using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Post, uri);
        httpRequest.Content = BackendHttpRequest.JsonContent(request);
        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            return new BackendFetchResult<BackendObjectCreateResponse>(false, (int)response.StatusCode, body, null);
        if (string.IsNullOrWhiteSpace(body))
            return new BackendFetchResult<BackendObjectCreateResponse>(false, (int)response.StatusCode, "Empty response.", null);
        var data = JsonSerializer.Deserialize<BackendObjectCreateResponse>(body, BackendHttpRequest.JsonOptions);
        return data is null
            ? new BackendFetchResult<BackendObjectCreateResponse>(false, (int)response.StatusCode, "Invalid response.", null)
            : new BackendFetchResult<BackendObjectCreateResponse>(true, (int)response.StatusCode, "OK", data);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
    {
        return new BackendFetchResult<BackendObjectCreateResponse>(false, null, ex.Message, null);
    }
}

public async Task<BackendSendResult> UpdateObjectAsync(
    string objectId,
    BackendObjectUpdateRequest request,
    CancellationToken cancellationToken)
{
    string path = $"/v1/service/objects/{Uri.EscapeDataString(objectId)}";
    if (!BackendHttpRequest.TryCreateUri(_settingsService, path, out Uri uri, out string message))
        return new BackendSendResult(false, null, message);

    try
    {
        using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Put, uri);
        httpRequest.Content = BackendHttpRequest.JsonContent(request);
        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        return response.IsSuccessStatusCode
            ? new BackendSendResult(true, (int)response.StatusCode, "OK")
            : new BackendSendResult(false, (int)response.StatusCode, body);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        return new BackendSendResult(false, null, ex.Message);
    }
}
```

- [ ] **Step 3: BackendSettingsViewModelTests.cs의 FakeCatalogClient에 새 메서드 스텁 추가**

`FakeCatalogClient` 클래스 내부(기존 `GetStoreListAsync` 아래)에 추가:

```csharp
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
```

- [ ] **Step 4: 빌드 + 테스트**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter/AnimatronicsControlCenter.csproj -p:Platform=x64
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj
```
Expected: Build succeeded, all existing tests pass.

- [ ] **Step 5: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IBackendServerCatalogClient.cs
git add AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendServerCatalogClient.cs
git add AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs
git commit -m "feat: extend catalog client with store/pc/object CRUD"
```

---

### Task 3: BackendRegistrationViewModel + 테스트

**Files:**
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendRegistrationViewModel.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendRegistrationViewModelTests.cs`

- [ ] **Step 1: BackendRegistrationViewModel.cs 생성**

경로: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendRegistrationViewModel.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    [ObservableProperty] private string dayOfWeek;
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

    public bool IsNewMode => !IsSelectMode;
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
    [ObservableProperty] private string editObjectOperationStatus = "PLAY";
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
             double.TryParse(NewStoreLatitude, out _) &&
             double.TryParse(NewStoreLongitude, out _) &&
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
    private void ToggleEdit() => IsEditExpanded = !IsEditExpanded;

    private async Task<bool> ProcessStep1Async()
    {
        if (IsSelectMode)
        {
            _resolvedStoreId = SelectedStore!.StoreId;
            _resolvedStoreName = SelectedStore.StoreName;
            _resolvedCountryCode = SelectedStore.CountryCode;

            if (IsEditExpanded && HasAnyStoreEdit())
            {
                double? lat = double.TryParse(EditStoreLatitude, out double latVal) ? latVal : null;
                double? lng = double.TryParse(EditStoreLongitude, out double lngVal) ? lngVal : null;
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
                    double.Parse(NewStoreLatitude), double.Parse(NewStoreLongitude),
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
                        NullIfEmpty(EditObjectName), timeRange, EditObjectScheduleFlag,
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
        !string.IsNullOrWhiteSpace(EditStoreName) ||
        !string.IsNullOrWhiteSpace(EditStoreCountryCode) ||
        !string.IsNullOrWhiteSpace(EditStoreAddress) ||
        !string.IsNullOrWhiteSpace(EditStoreLatitude) ||
        !string.IsNullOrWhiteSpace(EditStoreLongitude) ||
        !string.IsNullOrWhiteSpace(EditStoreTimezone);

    private bool HasAnyPcEdit() =>
        !string.IsNullOrWhiteSpace(EditPcName) || !string.IsNullOrWhiteSpace(EditPcSwVersion);

    private bool HasAnyObjectEdit() =>
        !string.IsNullOrWhiteSpace(EditObjectName) ||
        !string.IsNullOrWhiteSpace(EditObjectStartTime) ||
        !string.IsNullOrWhiteSpace(EditObjectEndTime) ||
        !string.IsNullOrWhiteSpace(EditObjectFirmwareBoardId) ||
        !string.IsNullOrWhiteSpace(EditObjectFirmwareBoardType) ||
        !string.IsNullOrWhiteSpace(EditObjectFirmwareVersion) ||
        !string.IsNullOrWhiteSpace(EditObjectOperationStatus) ||
        EditObjectScheduleFlag;

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
```

- [ ] **Step 2: 테스트 프로젝트 csproj에 컴파일 링크 추가**

`AnimatronicsControlCenter.Tests.csproj`의 `<ItemGroup>` (기존 `<Compile>` 항목들이 있는 곳) 끝에 추가:

```xml
<Compile Include="..\AnimatronicsControlCenter\UI\ViewModels\BackendRegistrationViewModel.cs" Link="UI\ViewModels\BackendRegistrationViewModel.cs" />
```

- [ ] **Step 3: BackendRegistrationViewModelTests.cs 작성**

경로: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendRegistrationViewModelTests.cs`

```csharp
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
```

- [ ] **Step 4: 빌드 + 테스트**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter/AnimatronicsControlCenter.csproj -p:Platform=x64
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj
```
Expected: 모든 테스트 통과.

- [ ] **Step 5: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendRegistrationViewModel.cs
git add AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj
git add AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendRegistrationViewModelTests.cs
git commit -m "feat: add BackendRegistrationViewModel with step wizard logic"
```

---

### Task 4: BackendRegistrationDialog XAML + 코드비하인드

**Files:**
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendRegistrationDialog.xaml`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendRegistrationDialog.xaml.cs`

> XAML에서 `bool → Visibility` 변환은 코드비하인드의 `BoolToVisibility` 메서드를 사용한다 (`DashboardPage.xaml.cs`와 동일 패턴).

- [ ] **Step 1: BackendRegistrationDialog.xaml 생성**

```xml
<ContentDialog
    x:Class="AnimatronicsControlCenter.UI.Views.BackendRegistrationDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:vm="using:AnimatronicsControlCenter.UI.ViewModels"
    Title="데이터 관리"
    CloseButtonText="취소"
    MinWidth="520">

    <ScrollViewer MaxHeight="620" VerticalScrollBarVisibility="Auto">
        <StackPanel Spacing="12" Padding="0,4,16,4">

            <!-- 단계 표시 -->
            <TextBlock Text="{x:Bind ViewModel.StepTitle, Mode=OneWay}"
                       Style="{StaticResource SubtitleTextBlockStyle}" />

            <!-- ==================== Step 1: Store ==================== -->
            <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsStep1Visible), Mode=OneWay}" Spacing="8">
                <StackPanel Orientation="Horizontal" Spacing="16">
                    <RadioButton Content="기존 선택"
                                 IsChecked="{x:Bind ViewModel.IsSelectMode, Mode=TwoWay}"
                                 GroupName="Step1Mode" />
                    <RadioButton Content="신규 등록"
                                 IsChecked="{x:Bind ViewModel.IsNewMode, Mode=TwoWay}"
                                 GroupName="Step1Mode" />
                </StackPanel>

                <!-- 기존 선택 패널 -->
                <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsSelectMode), Mode=OneWay}" Spacing="6">
                    <ComboBox Header="Store"
                              ItemsSource="{x:Bind ViewModel.AvailableStores}"
                              SelectedItem="{x:Bind ViewModel.SelectedStore, Mode=TwoWay}"
                              DisplayMemberPath="StoreName"
                              HorizontalAlignment="Stretch" />
                    <Button Content="수정하기 ▾"
                            Command="{x:Bind ViewModel.ToggleEditCommand}"
                            Visibility="{x:Bind BoolToVisibility(ViewModel.IsStoreSelected), Mode=OneWay}" />
                    <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsEditExpanded), Mode=OneWay}"
                                Spacing="6" Margin="0,4,0,0">
                        <TextBox Header="Store Name" Text="{x:Bind ViewModel.EditStoreName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                        <TextBox Header="Country Code" Text="{x:Bind ViewModel.EditStoreCountryCode, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                        <TextBox Header="Address" Text="{x:Bind ViewModel.EditStoreAddress, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <TextBox Header="Latitude" Text="{x:Bind ViewModel.EditStoreLatitude, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="220" />
                            <TextBox Header="Longitude" Text="{x:Bind ViewModel.EditStoreLongitude, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="220" />
                        </StackPanel>
                        <TextBox Header="Timezone" Text="{x:Bind ViewModel.EditStoreTimezone, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    </StackPanel>
                </StackPanel>

                <!-- 신규 등록 패널 -->
                <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsNewMode), Mode=OneWay}" Spacing="6">
                    <TextBox Header="Store Name *" Text="{x:Bind ViewModel.NewStoreName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <TextBox Header="Country Code *" Text="{x:Bind ViewModel.NewStoreCountryCode, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <TextBox Header="Address *" Text="{x:Bind ViewModel.NewStoreAddress, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <TextBox Header="Latitude *" Text="{x:Bind ViewModel.NewStoreLatitude, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="220" />
                        <TextBox Header="Longitude *" Text="{x:Bind ViewModel.NewStoreLongitude, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="220" />
                    </StackPanel>
                    <TextBox Header="Timezone *" Text="{x:Bind ViewModel.NewStoreTimezone, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <TextBlock Text="운영 시간 *" Margin="0,4,0,0" />
                    <ItemsRepeater ItemsSource="{x:Bind ViewModel.NewStoreOperateTimes}">
                        <ItemsRepeater.ItemTemplate>
                            <DataTemplate x:DataType="vm:OperateTimeEntry">
                                <StackPanel Orientation="Horizontal" Spacing="8" Margin="0,2">
                                    <TextBlock Text="{x:Bind DayOfWeek}" Width="40" VerticalAlignment="Center" />
                                    <TextBox Header="Open" Text="{x:Bind OpenTime, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="100" />
                                    <TextBox Header="Close" Text="{x:Bind CloseTime, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="100" />
                                </StackPanel>
                            </DataTemplate>
                        </ItemsRepeater.ItemTemplate>
                    </ItemsRepeater>
                </StackPanel>
            </StackPanel>

            <!-- ==================== Step 2: PC ==================== -->
            <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsStep2Visible), Mode=OneWay}" Spacing="8">
                <StackPanel Orientation="Horizontal" Spacing="16">
                    <RadioButton Content="기존 선택"
                                 IsChecked="{x:Bind ViewModel.IsSelectMode, Mode=TwoWay}"
                                 GroupName="Step2Mode" />
                    <RadioButton Content="신규 등록"
                                 IsChecked="{x:Bind ViewModel.IsNewMode, Mode=TwoWay}"
                                 GroupName="Step2Mode" />
                </StackPanel>

                <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsSelectMode), Mode=OneWay}" Spacing="6">
                    <ComboBox Header="PC"
                              ItemsSource="{x:Bind ViewModel.AvailablePcs, Mode=OneWay}"
                              SelectedItem="{x:Bind ViewModel.SelectedPc, Mode=TwoWay}"
                              DisplayMemberPath="PcName"
                              HorizontalAlignment="Stretch" />
                    <Button Content="수정하기 ▾"
                            Command="{x:Bind ViewModel.ToggleEditCommand}"
                            Visibility="{x:Bind BoolToVisibility(ViewModel.IsPcSelected), Mode=OneWay}" />
                    <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsEditExpanded), Mode=OneWay}" Spacing="6" Margin="0,4,0,0">
                        <TextBox Header="PC Name" Text="{x:Bind ViewModel.EditPcName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                        <TextBox Header="SW Version" Text="{x:Bind ViewModel.EditPcSwVersion, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    </StackPanel>
                </StackPanel>

                <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsNewMode), Mode=OneWay}" Spacing="6">
                    <TextBox Header="PC Name *" Text="{x:Bind ViewModel.NewPcName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <TextBox Header="SW Version *" Text="{x:Bind ViewModel.NewPcSwVersion, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                </StackPanel>
            </StackPanel>

            <!-- ==================== Step 3: Object ==================== -->
            <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsStep3Visible), Mode=OneWay}" Spacing="8">
                <StackPanel Orientation="Horizontal" Spacing="16">
                    <RadioButton Content="기존 선택"
                                 IsChecked="{x:Bind ViewModel.IsSelectMode, Mode=TwoWay}"
                                 GroupName="Step3Mode" />
                    <RadioButton Content="신규 등록"
                                 IsChecked="{x:Bind ViewModel.IsNewMode, Mode=TwoWay}"
                                 GroupName="Step3Mode" />
                </StackPanel>

                <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsSelectMode), Mode=OneWay}" Spacing="6">
                    <ComboBox Header="Object"
                              ItemsSource="{x:Bind ViewModel.AvailableObjects, Mode=OneWay}"
                              SelectedItem="{x:Bind ViewModel.SelectedObject, Mode=TwoWay}"
                              DisplayMemberPath="ObjectName"
                              HorizontalAlignment="Stretch" />
                    <Button Content="수정하기 ▾"
                            Command="{x:Bind ViewModel.ToggleEditCommand}"
                            Visibility="{x:Bind BoolToVisibility(ViewModel.IsObjectSelected), Mode=OneWay}" />
                    <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsEditExpanded), Mode=OneWay}" Spacing="6" Margin="0,4,0,0">
                        <TextBox Header="Object Name" Text="{x:Bind ViewModel.EditObjectName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                        <StackPanel Orientation="Horizontal" Spacing="8">
                            <TextBox Header="Start Time" Text="{x:Bind ViewModel.EditObjectStartTime, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="220" />
                            <TextBox Header="End Time" Text="{x:Bind ViewModel.EditObjectEndTime, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="220" />
                        </StackPanel>
                        <CheckBox Content="Schedule Flag" IsChecked="{x:Bind ViewModel.EditObjectScheduleFlag, Mode=TwoWay}" />
                        <ComboBox Header="Operation Status"
                                  ItemsSource="{x:Bind ViewModel.OperationStatusOptions}"
                                  SelectedItem="{x:Bind ViewModel.EditObjectOperationStatus, Mode=TwoWay}"
                                  HorizontalAlignment="Stretch" />
                        <TextBox Header="Firmware Board ID" Text="{x:Bind ViewModel.EditObjectFirmwareBoardId, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                        <TextBox Header="Firmware Board Type" Text="{x:Bind ViewModel.EditObjectFirmwareBoardType, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                        <TextBox Header="Firmware Version" Text="{x:Bind ViewModel.EditObjectFirmwareVersion, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    </StackPanel>
                </StackPanel>

                <StackPanel Visibility="{x:Bind BoolToVisibility(ViewModel.IsNewMode), Mode=OneWay}" Spacing="6">
                    <TextBox Header="Object Name *" Text="{x:Bind ViewModel.NewObjectName, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <StackPanel Orientation="Horizontal" Spacing="8">
                        <TextBox Header="Start Time *" Text="{x:Bind ViewModel.NewObjectStartTime, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="220" />
                        <TextBox Header="End Time *" Text="{x:Bind ViewModel.NewObjectEndTime, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" Width="220" />
                    </StackPanel>
                    <CheckBox Content="Schedule Flag" IsChecked="{x:Bind ViewModel.NewObjectScheduleFlag, Mode=TwoWay}" />
                    <ComboBox Header="Operation Status *"
                              ItemsSource="{x:Bind ViewModel.OperationStatusOptions}"
                              SelectedItem="{x:Bind ViewModel.NewObjectOperationStatus, Mode=TwoWay}"
                              HorizontalAlignment="Stretch" />
                    <TextBlock Text="Firmware" Margin="0,4,0,0" />
                    <TextBox Header="Board ID *" Text="{x:Bind ViewModel.NewObjectFirmwareBoardId, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <TextBox Header="Board Type *" Text="{x:Bind ViewModel.NewObjectFirmwareBoardType, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                    <TextBox Header="Version *" Text="{x:Bind ViewModel.NewObjectFirmwareVersion, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
                </StackPanel>
            </StackPanel>

            <!-- 내비게이션 버튼 -->
            <StackPanel Orientation="Horizontal" Spacing="8" HorizontalAlignment="Right" Margin="0,8,0,0">
                <Button Content="← 이전"
                        Command="{x:Bind ViewModel.GoBackCommand}"
                        Visibility="{x:Bind BoolToVisibility(ViewModel.IsBackVisible), Mode=OneWay}" />
                <Button Content="다음 →"
                        Command="{x:Bind ViewModel.GoNextCommand}"
                        Style="{StaticResource AccentButtonStyle}"
                        Visibility="{x:Bind BoolToVisibility(ViewModel.IsNextVisible), Mode=OneWay}" />
                <Button Content="완료 ✓"
                        Command="{x:Bind ViewModel.GoNextCommand}"
                        Style="{StaticResource AccentButtonStyle}"
                        Visibility="{x:Bind BoolToVisibility(ViewModel.IsFinishVisible), Mode=OneWay}" />
            </StackPanel>

            <!-- 상태 표시 -->
            <StackPanel Orientation="Horizontal" Spacing="8">
                <ProgressRing IsActive="{x:Bind ViewModel.IsBusy, Mode=OneWay}"
                              Width="20" Height="20" />
                <TextBlock Text="{x:Bind ViewModel.StatusMessage, Mode=OneWay}"
                           TextWrapping="Wrap"
                           VerticalAlignment="Center" />
            </StackPanel>

        </StackPanel>
    </ScrollViewer>
</ContentDialog>
```

- [ ] **Step 2: BackendRegistrationDialog.xaml.cs 생성**

```csharp
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class BackendRegistrationDialog : ContentDialog
{
    public BackendRegistrationViewModel ViewModel { get; }

    public BackendRegistrationDialog(BackendRegistrationViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        ViewModel.RequestClose += () => Hide();
    }

    private Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;
}
```

- [ ] **Step 3: 빌드로 XAML 컴파일 확인**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter/AnimatronicsControlCenter.csproj -p:Platform=x64
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendRegistrationDialog.xaml
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendRegistrationDialog.xaml.cs
git commit -m "feat: add BackendRegistrationDialog XAML wizard UI"
```

---

### Task 5: BackendSettingsPage 연결 + HandleRegistrationResultAsync

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendSettingsViewModel.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsPageXamlTests.cs`

- [ ] **Step 1: BackendSettingsPage.xaml에 "데이터 관리" 버튼 추가**

`BackendSettingsPage.xaml`의 ServerValuesPanel 내부, `<ListView .../>` 바로 아래에 추가:

```xml
<Button Content="데이터 관리"
        Click="OnOpenRegistrationDialogClicked"
        HorizontalAlignment="Stretch" />
```

- [ ] **Step 2: BackendSettingsPage.xaml.cs에 클릭 핸들러 추가**

파일 전체를 아래로 교체:

```csharp
using System.Collections.Generic;
using System.Linq;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class BackendSettingsPage : Page
{
    public BackendSettingsViewModel ViewModel { get; }

    public BackendSettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<BackendSettingsViewModel>();
    }

    private async void OnOpenRegistrationDialogClicked(object sender, RoutedEventArgs e)
    {
        var catalogClient = App.Current.Services.GetRequiredService<IBackendServerCatalogClient>();
        var stores = ViewModel.ServerStoreList.ToList();
        var vm = new BackendRegistrationViewModel(catalogClient, stores);
        var dialog = new BackendRegistrationDialog(vm);
        if (App.Current.m_window?.Content is FrameworkElement element)
            dialog.XamlRoot = element.XamlRoot;
        await dialog.ShowAsync();
        if (vm.Result is { } result)
            await ViewModel.HandleRegistrationResultAsync(result);
    }
}
```

- [ ] **Step 3: BackendSettingsViewModel에 _suppressStoreCascade 필드 및 HandleRegistrationResultAsync 추가**

`BackendSettingsViewModel.cs`에서 기존 `private BackendStoreDetailResponse? _lastFetchedStoreDetail;` 아래에 필드 추가:

```csharp
private bool _suppressStoreCascade = false;
```

`OnSelectedServerStoreChanged` 메서드를 아래로 교체:

```csharp
partial void OnSelectedServerStoreChanged(BackendStoreSummaryResponse? value)
{
    ServerPcList.Clear();
    SelectedServerPc = null;
    ServerObjects.Clear();
    _lastServerSnapshot = null;
    _lastFetchedStoreDetail = null;
    if (!_suppressStoreCascade && value is not null)
        _ = FetchStoreDetailForSelectionAsync(value.StoreId);
}
```

파일 끝(닫는 `}` 직전)에 메서드 추가:

```csharp
internal async Task HandleRegistrationResultAsync(RegistrationResult result)
{
    ServerStoreList.Clear();
    SelectedServerStore = null;

    if (!string.IsNullOrWhiteSpace(result.CountryCode))
        await FetchStoreListAsync(result.CountryCode);

    var store = ServerStoreList.FirstOrDefault(s => s.StoreId == result.StoreId);
    if (store is null) return;

    _suppressStoreCascade = true;
    SelectedServerStore = store;
    _suppressStoreCascade = false;

    await FetchStoreDetailForSelectionAsync(result.StoreId);
    SelectedServerPc = ServerPcList.FirstOrDefault(p => p.PcId == result.PcId);
}
```

- [ ] **Step 4: BackendSettingsViewModelTests.cs에 HandleRegistrationResult 테스트 추가**

`BackendSettingsViewModelTests` 클래스 내부에 테스트 메서드 추가:

```csharp
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
}
```

`BackendSettingsViewModelTests.cs` 상단 using에 추가:
```csharp
using AnimatronicsControlCenter.UI.ViewModels;
```

- [ ] **Step 5: BackendSettingsPageXamlTests.cs에 "데이터 관리" 확인 추가**

`BackendSettingsPage_ContainsServerAndLocalSettingsControls` 테스트의 `expected` 배열에 추가:

```csharp
"데이터 관리",
```

- [ ] **Step 6: 빌드 + 테스트 전체 실행**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter/AnimatronicsControlCenter.csproj -p:Platform=x64
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj
```
Expected: Build succeeded, 모든 테스트 통과.

- [ ] **Step 7: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml.cs
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendSettingsViewModel.cs
git add AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs
git add AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsPageXamlTests.cs
git commit -m "feat: wire up registration dialog and auto-refresh in BackendSettingsPage"
```

---

### Task 6: 최종 빌드 확인 + 브랜치 정리

- [ ] **Step 1: 전체 솔루션 빌드**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter.sln -p:Platform=x64
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 2: 전체 테스트 실행**

```
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj -v normal
```
Expected: 모든 테스트 통과.
