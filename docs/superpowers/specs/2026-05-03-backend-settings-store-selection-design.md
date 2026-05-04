# 백엔드 설정 페이지 - 스토어 선택 UI 개선 설계

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

왼쪽 패널에서 무작위 문자열인 Store ID를 직접 입력하는 방식을,  
서버에 등록된 스토어 목록에서 이름으로 선택하는 방식으로 교체한다.

---

## 전체 흐름 (상태 머신)

```
[초기 상태]
  Base URL + Bearer Token 입력 완료 시 Country Code 드롭다운 활성화

  ↓ Country Code 선택
  → GET /v1/service/stores?country_code={code}
  → 로딩 중: Store 드롭다운 "로딩 중..." + 비활성화
  → 성공: Store 드롭다운 활성화, 목록 채움 (store_name 표시)
  → 실패 / 빈 목록: 에러 메시지, Store 드롭다운 초기화

  ↓ Store 선택
  → GET /v1/service/stores/{store_id}/detail (기존 API 재사용)
  → 로딩 중: PC 드롭다운 "로딩 중..." + 비활성화
  → 성공: PC 드롭다운 활성화, 목록 채움 (pc_name 표시)
  → 실패: 에러 메시지, PC/Objects 초기화

  ↓ PC 선택
  → detail API 응답에 이미 포함된 Objects 목록 표시
  → "서버 값 적용 →" 버튼 활성화

  ↓ "서버 값 적용 →" 클릭
  → 오른쪽 로컬 설정 패널에 값 채움 (기존 ApplyServerValuesCommand 재사용)
```

Country Code 변경 시 → Store, PC, Objects 전부 초기화  
Store 변경 시 → PC, Objects 초기화

---

## API 변경

### 새 엔드포인트
```
GET /v1/service/stores?country_code={countryCode}
Authorization: Bearer {token}

Response: StoreListResponse
{
  "stores": [
    { "id": "0KPW9SYR11DF6", "store_name": "GM_하우스_도산", "country_code": "KR", ... }
  ]
}
```

### 기존 엔드포인트 (변경 없음)
```
GET /v1/service/stores/{store_id}/detail
→ store_name, country_code, pcs[{ pc_id, pc_name, sw_version, objects[] }]
```

---

## 코드 변경

### 1. `BackendDtos.cs` — 새 DTO 추가

```csharp
public sealed record BackendStoreListResponse(
    IReadOnlyList<BackendStoreSummaryResponse> Stores);

public sealed record BackendStoreSummaryResponse(
    string StoreId,
    string? StoreName,
    string? CountryCode);
```

### 2. `IBackendServerCatalogClient.cs` — 인터페이스 확장

```csharp
Task<BackendFetchResult<BackendStoreListResponse>> GetStoreListAsync(
    string countryCode,
    CancellationToken cancellationToken);
```

### 3. `BackendServerCatalogClient.cs` — 구현 추가

`GET {baseUrl}/v1/service/stores?country_code={countryCode}` 호출.  
기존 `GetStoreDetailAsync` 패턴과 동일한 방식으로 구현.

### 4. `BackendSettingsViewModel.cs` — 속성 및 커맨드 변경

**추가되는 속성:**

| 속성 | 타입 | 설명 |
|------|------|------|
| `AvailableCountryCodes` | `IReadOnlyList<string>` | 고정 목록: KR, JP, US, CN, GB |
| `SelectedCountryCode` | `string?` | 선택된 국가 코드. 변경 시 FetchStoreListCommand 자동 실행 |
| `ServerStoreList` | `ObservableCollection<BackendStoreSummaryResponse>` | 조회된 스토어 목록 |
| `SelectedServerStore` | `BackendStoreSummaryResponse?` | 선택된 스토어. 변경 시 FetchServerCommand 자동 실행 |
| `ServerPcList` | `ObservableCollection<BackendPcDetailResponse>` | 선택된 스토어의 PC 목록 |
| `SelectedServerPc` | `BackendPcDetailResponse?` | 선택된 PC. 변경 시 Objects 목록 갱신 |
| `IsFetchingStoreList` | `bool` | 스토어 목록 로딩 중 여부 |

**추가되는 커맨드:**
- `FetchStoreListCommand` — `SelectedCountryCode` setter에서 자동 호출

**변경되는 기존 동작:**
- `FetchServerCommand` → `SelectedServerStore` setter에서 자동 호출 (수동 버튼 제거)
- `SelectedServerPc` setter에서 `ServerObjects` 갱신 (기존 로직 이동)

### 5. `BackendSettingsPage.xaml` — UI 변경

**제거:**
- Store ID TextBox (수동 입력)
- "서버 조회" 버튼

**추가:**
- Country Code ComboBox (`AvailableCountryCodes` 바인딩)
- Store ComboBox (`ServerStoreList` 바인딩, `DisplayMemberPath="StoreName"`)
- PC ComboBox (`ServerPcList` 바인딩, `DisplayMemberPath="PcName"`)

**드롭다운 표시 형식:**
- Store: `GM_하우스_도산` (store_name)
- PC: `PC001` (pc_name)

---

## 에러 처리

| 상황 | 동작 |
|------|------|
| Base URL / Token 미입력 | Country Code 드롭다운 비활성화 |
| 스토어 목록 API 실패 | `ServerStatusMessage`에 에러 표시, 하위 드롭다운 초기화 |
| 해당 국가에 스토어 없음 | "해당 국가에 등록된 스토어가 없습니다" 메시지 |
| Store 상세 조회 실패 | PC/Objects 초기화, 에러 메시지 |
| 로딩 중 중복 요청 | 기존 요청 CancellationToken으로 취소 후 재요청 |

---

## 변경하지 않는 것

- 오른쪽 로컬 설정 패널 (Store ID, Store Name, Country Code 등 TextBox들)
- `ApplyServerValuesCommand` 로직
- `CompareWithServerCommand` 로직
- `SaveCommand` 로직
- `BackendSettingsComparison` 클래스
- `ISettingsService` 및 저장 형식

---

## 국가 코드 목록

코드에 상수로 분리하여 추후 확장 용이하게 관리:

```csharp
private static readonly IReadOnlyList<string> _countryCodes = ["KR", "JP", "US", "CN", "GB"];
```
