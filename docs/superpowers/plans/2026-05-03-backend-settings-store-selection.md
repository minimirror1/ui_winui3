# Backend Settings Store Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 백엔드 설정 페이지 왼쪽 패널에서 Store ID 직접 입력을 제거하고, 국가 코드 선택 → 스토어 선택 → PC 선택의 단계별 자동 연쇄 드롭다운으로 교체한다.

**Architecture:** Country Code ComboBox 선택 시 `GET /v1/service/stores?country_code=...` 호출, Store 선택 시 기존 `GET /v1/service/stores/{id}/detail` 재활용, PC 선택 시 이미 로드된 detail에서 Objects 목록 갱신. `_lastServerSnapshot`은 PC 선택 시점에 구성되며 기존 `ApplyServerValuesCommand`가 그대로 사용한다.

**Tech Stack:** C# 12, CommunityToolkit.Mvvm (ObservableProperty, RelayCommand, partial OnXxxChanged), WinUI3 x:Bind TwoWay, MSTest

---

## File Map

| 파일 | 변경 종류 | 역할 |
|------|-----------|------|
| `Core/Backend/BackendDtos.cs` | Modify | 새 DTO 추가 |
| `Core/Interfaces/IBackendServerCatalogClient.cs` | Modify | 새 메서드 시그니처 추가 |
| `Infrastructure/BackendServerCatalogClient.cs` | Modify | 새 메서드 구현 |
| `UI/ViewModels/BackendSettingsViewModel.cs` | Modify | 새 속성·명령·연쇄 로직 추가, 기존 FetchServerCommand 제거 |
| `UI/Views/BackendSettingsPage.xaml` | Modify | ComboBox 3개로 교체, 수동 입력·버튼 제거 |
| `Tests/BackendSettingsViewModelTests.cs` | Modify | 기존 테스트 교체 + 새 테스트 추가 |

---

## Task 1: BackendDtos에 스토어 목록 DTO 추가

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Backend/BackendDtos.cs`

- [ ] **Step 1: BackendDtos.cs 끝에 두 record 추가**

파일 끝(77번 줄 이후)에 다음을 추가한다:

```csharp
public sealed record BackendStoreListResponse(
    [property: JsonPropertyName("stores")] IReadOnlyList<BackendStoreSummaryResponse> Stores);

public sealed record BackendStoreSummaryResponse(
    [property: JsonPropertyName("id")] string StoreId,
    [property: JsonPropertyName("store_name")] string? StoreName,
    [property: JsonPropertyName("country_code")] string? CountryCode);
```

- [ ] **Step 2: 빌드 확인**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter/AnimatronicsControlCenter.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Backend/BackendDtos.cs
git commit -m "feat: add BackendStoreListResponse and BackendStoreSummaryResponse DTOs"
```

---

## Task 2: 인터페이스 확장 + FakeCatalogClient 업데이트

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IBackendServerCatalogClient.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs`

- [ ] **Step 1: IBackendServerCatalogClient에 메서드 추가**

`IBackendServerCatalogClient` 인터페이스에 다음을 추가한다 (기존 `GetStoreDetailAsync` 바로 아래):

```csharp
Task<BackendFetchResult<BackendStoreListResponse>> GetStoreListAsync(
    string countryCode,
    CancellationToken cancellationToken);
```

- [ ] **Step 2: 테스트 파일의 FakeCatalogClient에 메서드 추가**

`FakeCatalogClient` 클래스에 다음을 추가한다:

```csharp
public BackendStoreListResponse? StoreList { get; set; }

public Task<BackendFetchResult<BackendStoreListResponse>> GetStoreListAsync(
    string countryCode,
    CancellationToken cancellationToken)
{
    return Task.FromResult(new BackendFetchResult<BackendStoreListResponse>(
        StoreList is not null, 200, "OK", StoreList));
}
```

- [ ] **Step 3: 빌드 확인 (BackendServerCatalogClient 미구현으로 에러 예상)**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter/AnimatronicsControlCenter.csproj
```
Expected: CS0535 'BackendServerCatalogClient does not implement interface member GetStoreListAsync'. 이 에러는 Task 3에서 해결한다.

- [ ] **Step 4: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IBackendServerCatalogClient.cs
git add AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs
git commit -m "feat: add GetStoreListAsync to catalog client interface and fake"
```

---

## Task 3: GetStoreListAsync 클라이언트 구현

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendServerCatalogClient.cs`

- [ ] **Step 1: GetStoreListAsync 메서드 추가**

`BackendServerCatalogClient` 클래스에 다음 메서드를 추가한다 (`GetStoreDetailAsync` 바로 다음):

```csharp
public async Task<BackendFetchResult<BackendStoreListResponse>> GetStoreListAsync(
    string countryCode,
    CancellationToken cancellationToken)
{
    string path = $"/v1/service/stores?country_code={Uri.EscapeDataString(countryCode)}";
    if (!BackendHttpRequest.TryCreateUri(_settingsService, path, out Uri uri, out string message))
    {
        return new BackendFetchResult<BackendStoreListResponse>(false, null, message, null);
    }

    try
    {
        using HttpRequestMessage request = BackendHttpRequest.Create(_settingsService, HttpMethod.Get, uri);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new BackendFetchResult<BackendStoreListResponse>(false, (int)response.StatusCode, body, null);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return new BackendFetchResult<BackendStoreListResponse>(false, (int)response.StatusCode, "Backend response body is empty.", null);
        }

        var data = JsonSerializer.Deserialize<BackendStoreListResponse>(body, BackendHttpRequest.JsonOptions);
        return data is null
            ? new BackendFetchResult<BackendStoreListResponse>(false, (int)response.StatusCode, "Backend response body is invalid.", null)
            : new BackendFetchResult<BackendStoreListResponse>(true, (int)response.StatusCode, "OK", data);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
    {
        return new BackendFetchResult<BackendStoreListResponse>(false, null, ex.Message, null);
    }
}
```

- [ ] **Step 2: 빌드 확인**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter/AnimatronicsControlCenter.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendServerCatalogClient.cs
git commit -m "feat: implement GetStoreListAsync in BackendServerCatalogClient"
```

---

## Task 4: ViewModel — 국가 코드 선택 + 스토어 목록 (TDD)

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendSettingsViewModel.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

`BackendSettingsViewModelTests` 클래스에 다음 두 테스트를 추가한다:

```csharp
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

    viewModel.SelectedCountryCode = "JP";

    Assert.AreEqual(0, viewModel.ServerStoreList.Count);
    Assert.IsNull(viewModel.SelectedServerStore);
}
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

```
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj --filter "SelectedCountryCode"
```
Expected: FAIL — `SelectedCountryCode`, `ServerStoreList` 미존재로 컴파일 에러.

- [ ] **Step 3: ViewModel에 속성·메서드 구현**

`BackendSettingsViewModel.cs`에 다음을 추가한다.

필드 (`_serverCatalogClient` 필드 선언 바로 아래):
```csharp
private BackendStoreDetailResponse? _lastFetchedStoreDetail;
```

속성 (기존 `[ObservableProperty]` 블록 끝에 추가):
```csharp
[ObservableProperty] private string? selectedCountryCode;
[ObservableProperty] private BackendStoreSummaryResponse? selectedServerStore;
[ObservableProperty] private BackendPcDetailResponse? selectedServerPc;
[ObservableProperty] private bool isFetchingStoreList;

public static readonly IReadOnlyList<string> AvailableCountryCodes = ["KR", "JP", "US", "CN", "GB"];
public ObservableCollection<BackendStoreSummaryResponse> ServerStoreList { get; } = new();
public ObservableCollection<BackendPcDetailResponse> ServerPcList { get; } = new();
```

부분 메서드 + 비동기 fetch (클래스 끝, `TryParseMappings` 앞에 추가):
```csharp
partial void OnSelectedCountryCodeChanged(string? value)
{
    ServerStoreList.Clear();
    SelectedServerStore = null;
    ServerPcList.Clear();
    SelectedServerPc = null;
    ServerObjects.Clear();
    _lastServerSnapshot = null;
    _lastFetchedStoreDetail = null;
    if (value is not null)
        _ = FetchStoreListAsync(value);
}

private async Task FetchStoreListAsync(string countryCode)
{
    IsFetchingStoreList = true;
    ServerStatusMessage = string.Empty;
    var result = await _serverCatalogClient.GetStoreListAsync(countryCode, CancellationToken.None);
    IsFetchingStoreList = false;
    if (!result.Success || result.Data is null)
    {
        ServerStatusMessage = result.Message;
        return;
    }
    if (result.Data.Stores.Count == 0)
    {
        ServerStatusMessage = "해당 국가에 등록된 스토어가 없습니다.";
        return;
    }
    foreach (BackendStoreSummaryResponse store in result.Data.Stores)
        ServerStoreList.Add(store);
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인**

```
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj --filter "SelectedCountryCode"
```
Expected: 2 tests PASSED.

- [ ] **Step 5: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendSettingsViewModel.cs
git add AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs
git commit -m "feat: add country code selection and store list fetching to BackendSettingsViewModel"
```

---

## Task 5: ViewModel — 스토어 선택 → PC 목록 (TDD)

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendSettingsViewModel.cs`

- [ ] **Step 1: 실패하는 테스트 작성**

```csharp
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
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

```
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj --filter "SelectedServerStore"
```
Expected: FAIL — `OnSelectedServerStoreChanged` 미구현.

- [ ] **Step 3: ViewModel에 스토어 선택 핸들러 추가**

`OnSelectedCountryCodeChanged` 아래에 추가:

```csharp
partial void OnSelectedServerStoreChanged(BackendStoreSummaryResponse? value)
{
    ServerPcList.Clear();
    SelectedServerPc = null;
    ServerObjects.Clear();
    _lastServerSnapshot = null;
    _lastFetchedStoreDetail = null;
    if (value is not null)
        _ = FetchStoreDetailForSelectionAsync(value.StoreId);
}

private async Task FetchStoreDetailForSelectionAsync(string storeId)
{
    ServerStatusMessage = string.Empty;
    var result = await _serverCatalogClient.GetStoreDetailAsync(storeId, CancellationToken.None);
    if (!result.Success || result.Data is null)
    {
        ServerStatusMessage = result.Message;
        return;
    }
    _lastFetchedStoreDetail = result.Data;
    foreach (BackendPcDetailResponse pc in result.Data.Pcs)
        ServerPcList.Add(pc);
}
```

참고: `FakeCatalogClient.GetStoreDetailAsync`는 `StoreDetail`이 null이면 `Success=false`를 반환해야 한다. 기존 구현을 확인하고, 필요 시 테스트 파일의 FakeCatalogClient를 다음과 같이 수정한다:

```csharp
public Task<BackendFetchResult<BackendStoreDetailResponse>> GetStoreDetailAsync(
    string storeId, CancellationToken cancellationToken)
{
    bool success = StoreDetail is not null;
    string message = success ? "OK" : "Store not found.";
    return Task.FromResult(new BackendFetchResult<BackendStoreDetailResponse>(
        success, success ? 200 : 404, message, StoreDetail));
}
```

- [ ] **Step 4: 테스트 실행 — 통과 확인**

```
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj --filter "SelectedServerStore"
```
Expected: 3 tests PASSED.

- [ ] **Step 5: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendSettingsViewModel.cs
git add AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs
git commit -m "feat: add store selection to populate PC list in BackendSettingsViewModel"
```

---

## Task 6: ViewModel — PC 선택 → Snapshot + 기존 FetchServerCommand 제거 (TDD)

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendSettingsViewModel.cs`

- [ ] **Step 1: 기존 테스트 교체 + 새 테스트 작성**

`FetchServerCommand_SelectsCurrentPcSnapshot` 테스트를 **삭제**하고 다음 테스트들로 교체한다:

```csharp
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
```

- [ ] **Step 2: 테스트 실행 — 실패 확인**

```
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj --filter "SelectedServerPc|ApplyServerValues"
```
Expected: FAIL — `OnSelectedServerPcChanged` 미구현.

- [ ] **Step 3: ViewModel에 PC 선택 핸들러 추가**

`OnSelectedServerStoreChanged` 아래에 추가:

```csharp
partial void OnSelectedServerPcChanged(BackendPcDetailResponse? value)
{
    ServerObjects.Clear();
    _lastServerSnapshot = null;
    if (value is null || _lastFetchedStoreDetail is null) return;

    _lastServerSnapshot = new BackendServerSnapshot(
        _lastFetchedStoreDetail.StoreId,
        _lastFetchedStoreDetail.StoreName,
        _lastFetchedStoreDetail.CountryCode,
        value.PcId,
        value.PcName,
        value.SwVersion,
        value.Objects.Select(o => new BackendServerObjectSnapshot(o.Id, o.ObjectName)).ToArray());

    foreach (BackendServerObjectSnapshot obj in _lastServerSnapshot.Objects)
        ServerObjects.Add(obj);
}
```

`System.Linq` using이 없으면 추가: `using System.Linq;` (이미 있음).

- [ ] **Step 4: 기존 FetchServerAsync 및 관련 속성 제거**

ViewModel에서 다음을 제거한다:
- `[RelayCommand] private async Task FetchServerAsync()` 메서드 전체
- `[ObservableProperty] private string serverStoreId` 필드
- `[ObservableProperty] private string serverPcId` 필드

- [ ] **Step 5: 전체 테스트 실행 — 통과 확인**

```
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj
```
Expected: 모든 테스트 PASSED. (기존 테스트는 Task 5에서 FakeCatalogClient 업데이트로 이미 통과 상태)

- [ ] **Step 6: 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendSettingsViewModel.cs
git add AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs
git commit -m "feat: add PC selection snapshot logic, remove legacy FetchServerCommand"
```

---

## Task 7: XAML — 왼쪽 패널을 단계별 ComboBox로 교체

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml`

- [ ] **Step 1: ServerValuesPanel 전체를 교체**

`BackendSettingsPage.xaml`에서 `<StackPanel x:Name="ServerValuesPanel" ...>` 블록(13~23번 줄)을 다음으로 교체한다:

```xml
<StackPanel x:Name="ServerValuesPanel" Spacing="12">
    <TextBlock Text="Server Values" Style="{StaticResource SubtitleTextBlockStyle}" />
    <TextBox Header="Base URL"
             Text="{x:Bind ViewModel.BackendBaseUrl, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
    <PasswordBox Header="Bearer Token"
                 Password="{x:Bind ViewModel.BackendBearerToken, Mode=TwoWay}" />
    <ComboBox Header="Country Code"
              ItemsSource="{x:Bind ViewModel.AvailableCountryCodes}"
              SelectedItem="{x:Bind ViewModel.SelectedCountryCode, Mode=TwoWay}"
              HorizontalAlignment="Stretch" />
    <ComboBox Header="Store"
              ItemsSource="{x:Bind ViewModel.ServerStoreList, Mode=OneWay}"
              SelectedItem="{x:Bind ViewModel.SelectedServerStore, Mode=TwoWay}"
              DisplayMemberPath="StoreName"
              HorizontalAlignment="Stretch" />
    <ComboBox Header="PC"
              ItemsSource="{x:Bind ViewModel.ServerPcList, Mode=OneWay}"
              SelectedItem="{x:Bind ViewModel.SelectedServerPc, Mode=TwoWay}"
              DisplayMemberPath="PcName"
              HorizontalAlignment="Stretch" />
    <TextBlock Text="{x:Bind ViewModel.ServerStatusMessage, Mode=OneWay}"
               TextWrapping="Wrap" />
    <ListView ItemsSource="{x:Bind ViewModel.ServerObjects, Mode=OneWay}" />
</StackPanel>
```

제거되는 요소:
- `<TextBox Header="Store ID" ...>` (수동 입력)
- `<Button Content="서버 조회" ...>` 
- `<TextBlock Text="{x:Bind ViewModel.ServerStoreId, ...}">` 
- `<TextBlock Text="{x:Bind ViewModel.ServerPcId, ...}">`

- [ ] **Step 2: 빌드 확인 (XAML 컴파일 포함)**

```
dotnet build AnimatronicsControlCenter/AnimatronicsControlCenter/AnimatronicsControlCenter.csproj
```
Expected: Build succeeded, 0 errors.

만약 `ServerStoreId` 또는 `ServerPcId` binding 관련 에러가 나면 해당 속성이 ViewModel에서 완전히 제거되었는지 확인한다.

- [ ] **Step 3: 전체 테스트 최종 확인**

```
dotnet test AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj
```
Expected: 모든 테스트 PASSED.

- [ ] **Step 4: 최종 커밋**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml
git commit -m "feat: replace manual store ID input with cascading country/store/pc comboboxes"
```
