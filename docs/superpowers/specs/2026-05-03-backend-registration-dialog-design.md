# 백엔드 데이터 등록/수정 Dialog 설계

**날짜:** 2026-05-03  
**브랜치:** feature/backend-rest-monitoring  
**관련 파일:**
- `UI/Views/BackendSettingsPage.xaml`
- `UI/ViewModels/BackendSettingsViewModel.cs`
- `Core/Interfaces/IBackendServerCatalogClient.cs`
- `Infrastructure/BackendServerCatalogClient.cs`
- `Core/Backend/BackendDtos.cs`

---

## 목표

백엔드 설정 페이지 왼쪽 패널에 "데이터 관리" 버튼을 추가하고, 클릭 시 Store → PC → Object의 3단계 계단식 Wizard ContentDialog를 표시한다. 각 단계에서 기존 항목 선택 또는 신규 등록을 동시에 지원하며, 완료 후 드롭다운 목록을 새로고침하고 해당 항목을 자동 선택한다.

---

## UI 구조

### Dialog 진입점

`BackendSettingsPage.xaml` 왼쪽 패널(ServerValuesPanel)의 Objects ListView 아래에 "데이터 관리" 버튼 추가.

### Wizard 단계

```
[단계 1/3 — Store]
  ● 기존 선택  ○ 신규 등록

  [기존 선택 모드]
    Store ComboBox
    [수정하기 ▾] → 편집 폼 펼쳐짐 (Store Name, Country Code, Address,
                                    Latitude, Longitude, Timezone,
                                    운영시간 7행 표)

  [신규 등록 모드]
    Store Name*, Country Code*, Address*
    Latitude*, Longitude*, Timezone*
    운영시간 표 (MON~SUN × open_time ~ close_time)*

  [다음 →]  (유효성 통과 시 활성화)

────────────────────────────────────

[단계 2/3 — PC]
  ● 기존 선택  ○ 신규 등록

  [기존 선택 모드]
    PC ComboBox (Step 1에서 선택/생성된 Store의 PC 목록)
    [수정하기 ▾] → PC Name, SW Version 편집 폼

  [신규 등록 모드]
    PC Name*, SW Version*

  [← 이전]  [다음 →]

────────────────────────────────────

[단계 3/3 — Object]
  ● 기존 선택  ○ 신규 등록

  [기존 선택 모드]
    Object ComboBox (Step 2에서 선택/생성된 PC의 Object 목록)
    [수정하기 ▾] → Object Name, Start/End Time, Schedule Flag,
                    Operation Status, Firmware(Board ID/Type/Version) 편집 폼

  [신규 등록 모드]
    Object Name*, Start Time*, End Time*
    Schedule Flag, Operation Status* (PLAY/STOP/REPEAT ComboBox)
    Firmware Board ID*, Board Type*, Version*

  [← 이전]  [완료 ✓]
```

---

## 파일 구조

### 신규 생성

| 파일 | 역할 |
|------|------|
| `UI/Views/BackendRegistrationDialog.xaml` | 3단계 Wizard ContentDialog UI |
| `UI/Views/BackendRegistrationDialog.xaml.cs` | ContentDialog 코드비하인드 (ShowAsync 래퍼) |
| `UI/ViewModels/BackendRegistrationViewModel.cs` | 단계 상태, 폼 데이터, API 호출 로직 |

### 수정 대상

| 파일 | 변경 내용 |
|------|----------|
| `Core/Backend/BackendDtos.cs` | 누락 DTO 추가 (아래 목록 참조) |
| `Core/Interfaces/IBackendServerCatalogClient.cs` | 5개 메서드 추가 |
| `Infrastructure/BackendServerCatalogClient.cs` | 5개 메서드 구현 |
| `UI/ViewModels/BackendSettingsViewModel.cs` | `OpenRegistrationDialogCommand` 추가, 결과 처리 |
| `UI/Views/BackendSettingsPage.xaml` | "데이터 관리" 버튼 추가 |

---

## DTO 변경

### 추가 Request DTOs

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
```

### 추가 Response DTOs

```csharp
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

---

## IBackendServerCatalogClient 확장

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

---

## BackendRegistrationViewModel 핵심 상태

```csharp
// 내비게이션
int CurrentStep                   // 1, 2, 3
bool IsSelectMode                  // true=기존선택, false=신규등록
bool IsEditExpanded                // 기존선택에서 수정 폼 펼침

// Step 1 — Store
IReadOnlyList<BackendStoreSummaryResponse> AvailableStores  // 생성자 주입
BackendStoreSummaryResponse? SelectedStore
string NewStoreName, NewStoreCountryCode, NewStoreAddress
double NewStoreLatitude, NewStoreLongitude
string NewStoreTimezone
ObservableCollection<OperateTimeEntry> NewStoreOperateTimes // 7개 고정 행

// Step 2 — PC
ObservableCollection<BackendPcDetailResponse> AvailablePcs  // Step 1 결과에서
BackendPcDetailResponse? SelectedPc
string NewPcName, NewPcSwVersion

// Step 3 — Object
ObservableCollection<BackendObjectDetailResponse> AvailableObjects  // Step 2 결과에서
BackendObjectDetailResponse? SelectedObject
string NewObjectName, NewObjectStartTime, NewObjectEndTime
bool NewObjectScheduleFlag
string NewObjectOperationStatus       // PLAY/STOP/REPEAT
string NewObjectFirmwareBoardId, NewObjectFirmwareBoardType, NewObjectFirmwareVersion

// 진행 상태
bool IsBusy
string StatusMessage

// 결과 (Dialog 닫힌 후 BackendSettingsViewModel이 읽음)
RegistrationResult? Result
```

```csharp
public sealed record RegistrationResult(
    string StoreId,
    string? StoreName,
    string? CountryCode,
    string PcId,
    string? PcName,
    string ObjectId,
    string? ObjectName);
```

---

## 데이터 흐름

```
BackendSettingsViewModel.OpenRegistrationDialogCommand
  → BackendRegistrationDialog 생성 (AvailableStores = ServerStoreList 전달)
  → dialog.ShowAsync()
  → 완료 시 dialog.ViewModel.Result 읽음
  → Result.CountryCode로 FetchStoreListAsync 재호출 (드롭다운 갱신)
  → 갱신 완료 후 Result.StoreId, PcId 로 SelectedServerStore/Pc 자동 설정
```

---

## 유효성 검사 (단계 이동 조건)

| 단계 | 조건 |
|------|------|
| Step 1 → Step 2 | 기존선택: `SelectedStore != null` / 신규등록: Name·CountryCode·Address·Lat·Lng·Timezone·OperateTimes 모두 입력 |
| Step 2 → Step 3 | 기존선택: `SelectedPc != null` / 신규등록: PcName·SwVersion 입력 |
| Step 3 → 완료 | 기존선택: `SelectedObject != null` / 신규등록: 필수 필드 모두 입력 |

"다음/완료" 버튼은 조건 미충족 시 비활성화.

---

## 에러 처리

| 상황 | 동작 |
|------|------|
| API 호출 실패 | IsBusy 해제, StatusMessage에 오류 표시, 해당 단계 유지 |
| 이미 성공한 앞 단계 항목 | ID 보존, 재호출하지 않음 |
| 이전으로 돌아갈 때 | 입력값 유지, Step 1 Store 변경 시 Step 2/3 선택값 초기화 |

---

## 변경하지 않는 것

- `BackendSettingsComparison` 로직
- `SaveCommand`, `CompareWithServerCommand` 로직
- 오른쪽 로컬 설정 패널
- `ISettingsService` 및 저장 형식
