# 백엔드 REST 모니터링 연동 구현 계획

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**목표:** `ui_winui3` 앱에서 백엔드 REST API와 통신하여 매장/PC/오브제 식별 정보를 관리하고, 펌웨어에서 수신한 오브제 전원 상태와 상태 로그를 백엔드 서버로 보고할 수 있게 한다.

**아키텍처:** `HttpClient` 기반의 작은 typed REST client 계층을 추가하고, 서버 주소/토큰/Store ID/PC ID/Object ID 매핑은 왼쪽 하단의 지구본/서버 아이콘으로 여는 전용 `BackendSettingsPage`에서 편집한다. 이 화면은 서버 조회 영역과 로컬 설정 영역을 2분할로 보여주고, 서버에서 조회한 값을 로컬 설정으로 복사하거나 비교할 수 있게 한다. 설정값은 프로그램 실행파일 경로의 JSON 설정 파일에 저장한다. 펌웨어/장치 모델과 백엔드 DTO는 분리하고, Dashboard에 스캔된 모든 장치 상태를 백엔드 요청 DTO로 명시적으로 변환해 전역 동기화 서비스가 주기적으로 송신한다. `references/iic-robot-monitor-frontend`는 빌드 대상이 아닌 참고용으로만 사용한다.

**기술 스택:** WinUI 3, .NET `HttpClient`, `System.Text.Json`, 기존 `Microsoft.Extensions.DependencyInjection`, 기존 `SettingsService`, CommunityToolkit.Mvvm, MSTest.

## 구현 준비 상태

이 문서는 구현 가능한 계획이다. 아래 API 계약과 구현 방향은 다음 레퍼런스를 기준으로 조사했으며, 문서 끝의 리뷰 질문은 확정된 구현 결정과 검증 작업으로 반영했다.

- `references/iic-robot-monitor-frontend/api_doc.md`
- `references/iic-robot-monitor-frontend/frontend/src/lib/api/client.ts`
- `references/iic-robot-monitor-frontend/frontend/src/lib/api/stores.ts`
- `references/iic-robot-monitor-frontend/frontend/src/lib/api/pcs.ts`
- `references/iic-robot-monitor-frontend/frontend/src/lib/api/objects.ts`
- `references/iic-robot-monitor-frontend/frontend/src/lib/types/api.generated.ts`

## 레퍼런스 조사 결과

사용자가 제시한 아래 형태는 백엔드에 한 번에 POST하는 요청 body라기보다, `GET /v1/service/stores/{store_id}/detail`의 응답 구조에 가깝다.

```json
{
  "store_id": "store-1",
  "store_name": "Seoul Store",
  "pcs": [
    {
      "pc_id": "pc-1",
      "pc_name": "Main PC",
      "objects": [
        {
          "id": "obj-1",
          "object_name": "Robot A",
          "power_status": "ON",
          "error_data": []
        }
      ]
    }
  ]
}
```

백엔드 쓰기 API는 리소스 단위로 나뉘어 있다.

- Store 생성/수정: `POST /v1/service/stores`, `PUT /v1/service/stores/{store_id}`
- PC 생성/수정: `POST /v1/service/stores/{store_id}/pcs`, `PUT /v1/service/stores/{store_id}/pcs/{pc_id}`
- Object 생성/수정: `POST /v1/service/stores/{store_id}/pcs/{pc_id}/objects`, `PUT /v1/service/objects/{object_id}`
- 전원 제어 API: `POST /v1/service/objects/{object_id}/power`
  - 이 프로그램은 전원 제어 명령을 내리는 주체가 아니므로 v1에서는 호출하지 않는다.
  - 전원 상태 보고는 아래 상태/에러 로그 API의 `power_status` 필드로만 수행한다.
- 상태/에러 로그 송신: `POST /v1/service/objects/{object_id}/logs`

레퍼런스 프론트엔드의 기본 백엔드 URL:

```text
https://robot-monitor-api-dev.innergm.com
```

요청 기본값:

```text
Content-Type: application/json
Authorization: Bearer <token>  (문서상 optional, USER 권한 요청 시 필요)
Timeout: 15초
```

## 확정된 결정 사항

1. 백엔드 식별 정보의 출처
   - `ui_winui3`는 Store/PC/Object를 자동 생성하지 않는다.
   - 미리 생성된 백엔드 `store_id`, `pc_id`, `object_id`를 사용한다.
   - `ui_winui3` 프로그램 인스턴스 1개는 백엔드 PC 1개와 1:1 대응한다.
   - 같은 Store에 다른 PC가 있더라도, 해당 PC 정보는 그 PC에서 실행되는 다른 `ui_winui3` 인스턴스가 관리한다.
   - 이 프로그램은 설정된 `BackendPcId`에 해당하는 PC 1개만 조회/비교/보고 대상으로 삼는다.
   - Object에는 상태 로그만 보내고, PC에는 이 프로그램이 설치된 PC의 `pc_name`과 `sw_version` 메타데이터만 갱신한다.

2. 오브제 매핑 방식
   - 스캔된 animatronics 장치 1개가 백엔드 Object 1개에 대응한다.
   - 매핑 키는 로컬 장치 ID다. 예: device `2` -> `DeviceObjectMappings[2] = "obj-..."`.
   - `Store ID`, `PC ID`, `Object ID` 매핑은 왼쪽 하단 지구본/서버 아이콘으로 여는 전용 백엔드 설정 화면에서 편집한다.
   - 설정 파일은 프로그램 실행파일 경로에 저장한다.

3. 상태/로그 송신 트리거
   - Device Detail 화면에 들어간 장치만 보내지 않는다.
   - Dashboard에 스캔되어 등록된 모든 장치에 대해 전역 background loop가 주기적으로 상태를 갱신하고 백엔드로 송신한다.

4. `operation_status` 매핑
   - `MotionState.Playing` -> `"PLAY"`
   - `MotionState.Stopped`, `MotionState.Idle`, `MotionState.Paused` -> `"STOP"`

5. `power_status` 출처
   - 연결 상태로 추정하지 않는다.
   - 펌웨어가 PONG payload에 담아 제공할 전원 상태를 사용해 `"ON"` 또는 `"OFF"`로 보낸다.
   - 구현 시 PONG payload의 전원 상태 필드를 `Device.PowerStatus`로 투영한다.
   - 백엔드 보고는 `POST /v1/service/objects/{object_id}/logs`의 `power_status` 필드로 수행한다.
   - `POST /v1/service/objects/{object_id}/power`는 전원 제어 API이므로 이 프로그램에서는 호출하지 않는다.
   - ping 실패 시에는 연결 끊김으로 판단하고 `"OFF"` 로그를 보낸다.
   - 실제 전원 OFF와 통신 실패 OFF를 구분하기 위해 ping 실패 로그에는 `error_data`에 `"Disconnected"`를 함께 포함한다.

6. `error_data` 출처
   - 아래 “에러 매핑 제안”을 v1 기본값으로 사용한다.
   - ping 실패처럼 장치 응답 자체가 없는 경우에는 연결 끊김 error_data를 추가한다.

7. PC 기본값
   - `pc_name`: 기본값 `"pc_name_001"`, 전용 백엔드 설정 화면에서 수정 가능해야 한다.
   - `sw_version`: 이 PC에 설치된 `ui_winui3` 프로그램 버전이다.
   - `sw_version` 기본값은 `"1.1.1.0"`이고, 전용 백엔드 설정 화면에서 수정 가능해야 한다.
   - 저장된 `sw_version`은 백엔드의 기존 PC 메타데이터에 보고한다.
   - PC 자동 생성은 하지 않으며, 미리 생성된 `store_id`/`pc_id`에 대해 `PUT /v1/service/stores/{store_id}/pcs/{pc_id}`로 `pc_name`과 `sw_version`만 갱신한다.

8. PONG 전원 상태 wire format
   - v1 wire format은 기존 PONG payload 끝에 전원 상태 1 byte를 선택적으로 추가하는 방식으로 확정한다.
   - `0x01`은 `"ON"`, `0x00`은 `"OFF"`로 해석한다.
   - 기존 펌웨어처럼 추가 byte가 없는 PONG payload는 호환성을 위해 정상 파싱하고 `Device.PowerStatus = "OFF"` 기본값을 유지한다.
   - 알 수 없는 byte 값은 `"OFF"`로 처리하고, binary parser가 예외를 던지지 않게 한다.

9. 실제 서버 검증 범위
   - `POST /v1/service/objects/{object_id}/logs`가 성공하더라도 서버 detail 응답의 최신 `power_status` 반영 여부는 서버 동작에 의존한다.
   - 구현은 `/logs` 계약에 맞춰 완료하고, 실제 반영 여부는 Task 11의 수동 스모크 테스트에서 확인한다.
   - 스모크 테스트 실패는 REST 클라이언트 구현 실패와 서버 계약/환경 문제를 구분해 기록한다.

## 에러 매핑 제안

현재 앱의 `MotorState`에는 `GroupId`, `SubId`, `Type`, `Status`가 있다. 백엔드 `error_data`는 `{ boardId, boardType, errorCode }` 형태이므로 v1에서는 모터 상태를 아래처럼 변환한다.

- 정상 상태는 송신하지 않는다.
  - `Status == "Normal"`이면 `error_data`에 포함하지 않는다.
- 오류 상태만 송신한다.
  - `Status == "Error"`, `"Overload"`, `"Disconnected"`이면 `error_data`에 포함한다.
- ping 실패/장치 무응답은 실제 전원 OFF와 구분하기 위해 별도 연결 끊김 오류로 송신한다.
  - `boardId`: 로컬 장치 ID 문자열. 예: device 2 -> `"2"`.
  - `boardType`: `"DEVICE"`.
  - `errorCode`: `"Disconnected"`.
- `boardId`
  - `GroupId`와 `SubId`가 있으면 `"{GroupId}-{SubId}"`로 보낸다. 예: group 1, sub 2 -> `"1-2"`.
  - 둘 중 하나가 없으면 `MotorState.Id`를 문자열로 보낸다.
- `boardType`
  - `MotorState.Type`을 그대로 보낸다. 예: `"RC"`, `"AC"`, `"BL"`, `"DXL"`, `"ZER"`, `"AC2"`.
- `errorCode`
   - `Status` 문자열을 그대로 보낸다. 예: `"Error"`, `"Overload"`, `"Disconnected"`.
   - 백엔드에는 숫자 코드가 아니라 문자열로 전송한다.

예시:

```json
{
  "boardId": "1-2",
  "boardType": "AC",
  "errorCode": "Error"
}
```

이 방식의 장점은 현재 앱 모델만으로 구현할 수 있고, 사람이 보기에 원인을 바로 추적할 수 있다는 점이다. 나중에 펌웨어가 숫자형 board error code를 제공하면 `errorCode`만 펌웨어 코드로 교체한다.

## HTTP 공통 처리 규칙

- 모든 REST 요청은 현재 `ISettingsService.BackendBaseUrl`을 읽어 absolute URI로 만든다.
- `BackendBaseUrl` 앞뒤 공백은 제거하고, 끝의 `/`는 endpoint path와 중복되지 않게 처리한다.
- `BackendBaseUrl`이 비어 있거나 absolute URI가 아니면 네트워크 호출 없이 실패 결과를 반환한다.
- token은 요청마다 `HttpRequestMessage.Headers.Authorization`에 설정한다.
- token이 비어 있거나 공백이면 Authorization 헤더를 보내지 않는다.
- `HttpClient.BaseAddress`와 `HttpClient.DefaultRequestHeaders.Authorization`은 사용하지 않는다.
- JSON 직렬화/역직렬화는 `JsonSerializerDefaults.Web` 옵션을 사용한다.
- 성공 판정은 HTTP 2xx다.
- non-2xx 응답은 status code와 응답 body 일부를 `BackendSendResult` 또는 `BackendFetchResult<T>`의 message에 담는다.
- `HttpRequestException`, `TaskCanceledException`, `JsonException`, 잘못된 설정 값은 호출자를 중단시키지 않고 실패 결과로 변환한다.
- 백엔드 실패는 serial/XBee 통신, Dashboard polling, UI navigation을 중단시키지 않는다.
- 응답 body가 비어 있는 성공 응답은 송신 계열에서는 성공으로 처리하고, 조회 계열에서는 데이터 없음 실패로 처리한다.

## 제안 DTO

생성 파일: `AnimatronicsControlCenter/Core/Backend/BackendDtos.cs`

```csharp
using System.Text.Json.Serialization;

namespace AnimatronicsControlCenter.Core.Backend;

public sealed record BackendStoreCreateRequest(
    [property: JsonPropertyName("store_name")] string StoreName,
    [property: JsonPropertyName("country_code")] string CountryCode,
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("timezone")] string Timezone,
    [property: JsonPropertyName("operate_times")] IReadOnlyList<BackendStoreOperateTime> OperateTimes);

public sealed record BackendStoreOperateTime(
    [property: JsonPropertyName("day_of_week")] string DayOfWeek,
    [property: JsonPropertyName("open_time")] string OpenTime,
    [property: JsonPropertyName("close_time")] string CloseTime);

public sealed record BackendPcCreateRequest(
    [property: JsonPropertyName("pc_name")] string PcName,
    [property: JsonPropertyName("sw_version")] string SwVersion);

public sealed record BackendPcUpdateRequest(
    [property: JsonPropertyName("pc_name")] string PcName,
    [property: JsonPropertyName("sw_version")] string SwVersion);

public sealed record BackendObjectCreateRequest(
    [property: JsonPropertyName("object_name")] string ObjectName,
    [property: JsonPropertyName("object_operation_time")] BackendTimeRange ObjectOperationTime,
    [property: JsonPropertyName("schedule_flag")] bool ScheduleFlag,
    [property: JsonPropertyName("firmware_version")] BackendFirmwareVersion FirmwareVersion,
    [property: JsonPropertyName("operation_status")] string OperationStatus);

public sealed record BackendTimeRange(
    [property: JsonPropertyName("start_time")] string StartTime,
    [property: JsonPropertyName("end_time")] string EndTime);

public sealed record BackendFirmwareVersion(
    [property: JsonPropertyName("board_id")] string BoardId,
    [property: JsonPropertyName("board_type")] string BoardType,
    [property: JsonPropertyName("version")] string Version);

public sealed record BackendObjectLogRequest(
    [property: JsonPropertyName("power_status")] string PowerStatus,
    [property: JsonPropertyName("operation_status")] string OperationStatus,
    [property: JsonPropertyName("power_consumption")] BackendPowerConsumption? PowerConsumption,
    [property: JsonPropertyName("error_data")] IReadOnlyList<BackendErrorData> ErrorData);

public sealed record BackendPowerConsumption(
    [property: JsonPropertyName("volt")] string? Volt,
    [property: JsonPropertyName("ampere")] string? Ampere,
    [property: JsonPropertyName("watt")] string? Watt);

public sealed record BackendErrorData(
    [property: JsonPropertyName("boardId")] string? BoardId,
    [property: JsonPropertyName("boardType")] string? BoardType,
    [property: JsonPropertyName("errorCode")] string? ErrorCode);

public sealed record BackendStoreDetailResponse(
    [property: JsonPropertyName("store_id")] string StoreId,
    [property: JsonPropertyName("store_name")] string? StoreName,
    [property: JsonPropertyName("country_code")] string? CountryCode,
    [property: JsonPropertyName("pcs")] IReadOnlyList<BackendPcDetailResponse> Pcs);

public sealed record BackendPcDetailResponse(
    [property: JsonPropertyName("pc_id")] string PcId,
    [property: JsonPropertyName("pc_name")] string? PcName,
    [property: JsonPropertyName("sw_version")] string? SwVersion,
    [property: JsonPropertyName("objects")] IReadOnlyList<BackendObjectDetailResponse> Objects);

public sealed record BackendObjectDetailResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object_name")] string? ObjectName,
    [property: JsonPropertyName("power_status")] string? PowerStatus,
    [property: JsonPropertyName("error_data")] IReadOnlyList<BackendErrorData>? ErrorData);
```

## 장치 모델 추가 제안

펌웨어가 전원 상태를 제공하므로 `Device` 모델에 전원 상태를 명시적으로 추가한다.

수정 파일: `AnimatronicsControlCenter/Core/Models/Device.cs`

```csharp
[ObservableProperty]
private string powerStatus = "OFF"; // backend values: ON/OFF
```

투영 규칙:

- PONG payload에서 `"ON"`/`"OFF"`에 해당하는 전원 상태를 읽어 `Device.PowerStatus`에 저장한다.
- 펌웨어 응답을 받지 못한 장치는 기본값 `"OFF"`로 둔다.
- `BackendDeviceMapper`는 연결 상태가 아니라 `Device.PowerStatus`를 사용한다.

PONG payload 확장 구현:

- 현재 저장소의 binary PONG payload에는 전원 상태 필드가 아직 보이지 않는다.
- 전원 상태는 기존 PONG payload 끝의 선택적 1 byte로 처리하므로 `PongStatus`, `BinaryDeserializer.TryParsePongResponse`, `FirmwareStatusProjection.Apply`를 함께 확장한다.
- 기존 펌웨어와의 호환을 위해 전원 상태 필드가 없는 기존 길이의 PONG payload도 계속 파싱하고, 이 경우 `Device.PowerStatus = "OFF"` 기본값을 사용한다.

## 제안 인터페이스

생성 파일: `AnimatronicsControlCenter/Core/Interfaces/IBackendSettingsPathProvider.cs`

```csharp
namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendSettingsPathProvider
{
    string BackendSettingsFilePath { get; }
}
```

생성 파일: `AnimatronicsControlCenter/Core/Interfaces/IBackendMonitoringService.cs`

```csharp
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendMonitoringService
{
    Task<BackendSendResult> SendObjectLogAsync(Device device, CancellationToken cancellationToken);
}

public sealed record BackendSendResult(bool Success, int? StatusCode, string Message);
```

생성 파일: `AnimatronicsControlCenter/Core/Interfaces/IBackendServerCatalogClient.cs`

```csharp
using AnimatronicsControlCenter.Core.Backend;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendServerCatalogClient
{
    Task<BackendFetchResult<BackendStoreDetailResponse>> GetStoreDetailAsync(
        string storeId,
        CancellationToken cancellationToken);

    Task<BackendSendResult> UpdatePcMetadataAsync(
        string storeId,
        string pcId,
        BackendPcUpdateRequest request,
        CancellationToken cancellationToken);
}

public sealed record BackendFetchResult<T>(
    bool Success,
    int? StatusCode,
    string Message,
    T? Data);
```

생성 파일: `AnimatronicsControlCenter/Core/Interfaces/IBackendObjectIdResolver.cs`

```csharp
namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendObjectIdResolver
{
    string? ResolveObjectId(int deviceId);
}
```

생성 파일: `AnimatronicsControlCenter/Core/Interfaces/IBackendDashboardSyncService.cs`

```csharp
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendDashboardSyncService
{
    void ReplaceDevices(IEnumerable<Device> devices);
    void Start();
    void Stop();
}
```

## 제안 설정 항목

`ISettingsService`와 `SettingsService`에 다음 백엔드 전용 값을 추가한다. UI는 기존 일반 Settings 페이지가 아니라 전용 `BackendSettingsPage`에서 이 값을 읽고 저장한다.

```csharp
bool IsBackendSyncEnabled { get; set; }
string BackendBaseUrl { get; set; }              // 기본값: https://robot-monitor-api-dev.innergm.com
string BackendBearerToken { get; set; }          // 빈 값이면 Authorization 헤더 없음
string BackendStoreId { get; set; }
string BackendStoreName { get; set; }
string BackendStoreCountryCode { get; set; }     // 예: KR
string BackendPcId { get; set; }
string BackendPcName { get; set; }               // 기본값: pc_name_001
string BackendSoftwareVersion { get; set; }      // 기본값: 1.1.1.0
Dictionary<int, string> BackendDeviceObjectMappings { get; set; } // 예: [2] = "obj-1"
int BackendSyncIntervalSeconds { get; set; }     // 기본값: 5
```

설정 저장 정책:

- 기존 `SettingsService`의 `ApplicationData.Current.LocalSettings`에는 저장하지 않는다.
- 백엔드 설정은 프로그램 실행파일 경로에 JSON 파일로 저장한다.
- `IsBackendSyncEnabled`도 같은 JSON 파일에 저장하고 로드한다. 사용자가 활성/비활성 상태를 바꾸면 다음 프로그램 실행 시에도 그 상태를 유지한다.
- `backendDeviceObjectMappings`는 설정 파일에서도 JSON object로 저장하고, `SettingsService`에서도 `Dictionary<int,string>`로 유지한다.
- JSON 문자열은 설정 서비스의 핵심 타입으로 쓰지 않는다. 사용자가 textarea에서 직접 편집하는 경우에만 `BackendSettingsViewModel`의 임시 문자열 draft로 둔다.
- 제안 파일명: `backend-settings.json`
- 제안 경로: `Path.Combine(AppContext.BaseDirectory, "backend-settings.json")`
- 실행파일 경로가 쓰기 불가능한 배포 형태라면 저장 실패를 사용자에게 보여준다. 단, 이번 요구사항의 기본 경로는 실행파일 경로로 고정한다.

기본 정책:

- 백엔드 동기화는 기본 활성화.
- 설정 파일이 없거나 `isBackendSyncEnabled` 값이 없으면 `IsBackendSyncEnabled == true`로 시작한다.
- `IsBackendSyncEnabled`는 Dashboard 전역 background 상태 송신만 제어한다.
- 사용자가 비활성화로 저장한 경우에는 다음 실행부터 `IsBackendSyncEnabled == false`로 로드하고 Dashboard 자동 `/logs` 송신 loop를 돌리지 않는다.
- 백엔드 설정 화면의 `서버 조회`, `서버 값 비교`, `저장 후 PC metadata 보고`는 사용자가 명시적으로 누르는 수동 REST 호출이므로 `IsBackendSyncEnabled`와 별개로 동작한다.
- 장치에 대응하는 Object ID가 없으면 송신하지 않고 상태 메시지만 남긴다.
- v1에서는 Store/PC/Object 자동 생성은 하지 않는다.
- `ui_winui3` 인스턴스 1개는 `BackendPcId` 1개만 담당한다.
- Store detail 응답의 다른 PC들은 조회 결과에 포함되어도 이 프로그램의 비교/보고 대상이 아니다.
- Dashboard에 등록된 모든 스캔 장치가 동기화 대상이다.
- `BackendPcName` 기본값은 `"pc_name_001"`이다.
- `BackendSoftwareVersion` 기본값은 `"1.1.1.0"`이며, 이 PC에 설치된 `ui_winui3`의 `sw_version`으로 서버에 보고한다.
- `BackendStoreCountryCode`는 서버 조회 결과에 `country_code`가 있으면 비교/복사 대상이고, 없으면 사용자가 직접 입력한다.

## 백엔드 설정 화면 제안

이 기능은 기존 Settings 페이지에 섞지 않고 별도 화면으로 추가한다. 설정 버튼의 USB/COM 포트 처리 지연과 무관하게 열려야 하므로, `NavigationView.PaneFooter`의 왼쪽 하단 영역에 지구본/서버 아이콘 버튼을 추가하고 클릭 시 즉시 `BackendSettingsPage`로 전환한다. 현재 `MainWindow.xaml`의 `PaneFooter`에는 serial traffic 버튼이 있으므로, footer를 세로 `StackPanel` 또는 `Grid`로 바꿔 백엔드 설정 버튼을 serial traffic 버튼 위 또는 아래에 배치한다.

화면 구조:

- 왼쪽 분할: 서버 값 영역
  - `BackendBaseUrl`, `BackendBearerToken`, 조회할 `Store ID` 입력.
  - `서버 조회` 버튼으로 `GET /v1/service/stores/{store_id}/detail` 호출.
  - 조회 결과의 Store, PC, Object 목록을 읽기 전용으로 표시한다.
  - v1에서는 Store/PC/Object 생성/수정 REST 호출을 하지 않는다. 상태 송신은 Dashboard 전역 동기화에서 처리한다.
- 가운데: 서버 값 적용 버튼
  - 버튼명 제안: `서버 값 적용`.
  - 서버에서 조회한 현재 선택 Store/PC/Object 값을 오른쪽 로컬 설정 draft로 복사한다.
  - 복사는 저장이 아니며, 사용자가 오른쪽 `저장`을 눌러야 `backend-settings.json`에 반영된다.
- 오른쪽 분할: 로컬 소프트웨어 설정 영역
  - 편집 항목: Store ID, Store Name, Store Country Code, PC ID, PC Name, Software Version, device/object mapping, sync enabled, sync interval.
  - `저장` 버튼은 실행파일 경로의 `backend-settings.json`에 저장하고 다음 실행 시 로드한다.
  - `Software Version`은 이 PC에 설치된 `ui_winui3` 버전이며, 저장 후 기존 백엔드 PC의 `sw_version`으로 보고한다.
  - PC 메타데이터 보고는 미리 생성된 PC에 대한 갱신만 수행한다. Store/PC/Object 자동 생성은 하지 않는다.
  - 비교 버튼명 제안: `서버 값 비교`.
  - 비교 결과는 오른쪽 입력창별로 표시한다. 같은 값은 상단 요약에만 포함하고, 다른 값은 해당 입력창 아래에 빨간 메시지로 표시한다.

비교 메시지 규칙:

- 로컬 값이 비어 있음: `로컬 설정값이 비어 있습니다. 서버 값은 '<server>'입니다.`
- 서버 값이 없음: `서버 조회 결과에 이 값이 없습니다. Store/PC/Object ID를 확인하세요.`
- 값 불일치: `서버 값 '<server>'와 로컬 값 '<local>'이 다릅니다.`
- 서버에 PC ID 없음: `조회한 Store 아래에 이 PC ID가 없습니다.`
- Object ID 매핑 없음: `로컬 장치 <deviceId>에 대응하는 서버 Object ID가 없습니다.`
- Object ID가 서버 목록에 없음: `서버의 선택 PC 아래에 Object ID '<objectId>'가 없습니다.`
- 서버 조회 실패 또는 오래된 조회 결과: `서버 값을 먼저 조회해야 비교할 수 있습니다.`

이 화면은 백엔드 설정 파일과 REST 조회 클라이언트가 있어야 의미 있게 동작하므로, 구현 순서는 `DTO/설정 파일/HTTP 조회 클라이언트/비교 모델` 다음, `Dashboard 전역 동기화` 이전에 둔다.

## 구현 태스크

아래 순서대로 실행한다. 각 Task는 RED 확인, 최소 구현, GREEN 확인 후 커밋한다. 앞 Task의 public contract를 뒤 Task가 사용하므로 순서를 바꾸지 않는다.

권장 커밋 단위:

- Task 1: `test: add backend DTO serialization coverage` + `feat: add backend DTO contracts`
- Task 2: `feat: persist backend settings to executable path json`
- Task 3: `feat: resolve backend object ids from device mappings`
- Task 4: `feat: add backend REST clients`
- Task 5: `feat: compare backend server and local settings`
- Task 6: `feat: add backend settings navigation entry`
- Task 7: `feat: add backend settings page`
- Task 8: `feat: map device status to backend logs`
- Task 9: `chore: register backend services`
- Task 10: `feat: sync dashboard devices to backend`
- Task 11: `test: document backend server smoke verification`

각 커밋 전에는 해당 Task의 filtered test를 먼저 실행하고, 마지막에는 전체 테스트와 x64 build를 실행한다.

### Task 1: 백엔드 DTO 직렬화 테스트 추가

**파일:**
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendDtoSerializationTests.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Backend/BackendDtos.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj`

**Step 1: 실패 테스트 작성**

백엔드 JSON 필드명이 정확히 나가는지 테스트한다.

```csharp
using System.Text.Json;
using AnimatronicsControlCenter.Core.Backend;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendDtoSerializationTests
{
    [TestMethod]
    public void ObjectLogRequest_UsesBackendJsonFieldNames()
    {
        var request = new BackendObjectLogRequest(
            PowerStatus: "ON",
            OperationStatus: "STOP",
            PowerConsumption: null,
            ErrorData: Array.Empty<BackendErrorData>());

        string json = JsonSerializer.Serialize(request);

        StringAssert.Contains(json, "\"power_status\":\"ON\"");
        StringAssert.Contains(json, "\"operation_status\":\"STOP\"");
        StringAssert.Contains(json, "\"error_data\":[]");
    }

    [TestMethod]
    public void PcUpdateRequest_UsesBackendJsonFieldNames()
    {
        string json = JsonSerializer.Serialize(new BackendPcUpdateRequest("pc_name_001", "1.1.1.0"));

        StringAssert.Contains(json, "\"pc_name\":\"pc_name_001\"");
        StringAssert.Contains(json, "\"sw_version\":\"1.1.1.0\"");
    }

    [TestMethod]
    public void StoreDetailResponse_ReadsBackendJsonFieldNames()
    {
        const string json = """
        {
          "store_id": "store-1",
          "store_name": "Seoul Store",
          "country_code": "KR",
          "pcs": [
            {
              "pc_id": "pc-1",
              "pc_name": "Main PC",
              "sw_version": "1.1.1.0",
              "objects": [
                { "id": "obj-1", "object_name": "Robot A", "power_status": "ON", "error_data": [] }
              ]
            }
          ]
        }
        """;

        var response = JsonSerializer.Deserialize<BackendStoreDetailResponse>(json);

        Assert.AreEqual("store-1", response!.StoreId);
        Assert.AreEqual("KR", response.CountryCode);
        Assert.AreEqual("pc-1", response.Pcs[0].PcId);
        Assert.AreEqual("1.1.1.0", response.Pcs[0].SwVersion);
        Assert.AreEqual("obj-1", response.Pcs[0].Objects[0].Id);
    }
}
```

**Step 2: 테스트를 실행해 RED 확인**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.Tests\AnimatronicsControlCenter.Tests.csproj --filter BackendDtoSerializationTests
```

예상: `BackendObjectLogRequest`, `BackendStoreDetailResponse` 등이 없어서 실패.

**Step 3: DTO와 테스트 프로젝트 링크 추가**

`Core/Backend/BackendDtos.cs`를 만들고 테스트 프로젝트에 다음 include를 추가한다.

```xml
<Compile Include="..\AnimatronicsControlCenter\Core\Backend\BackendDtos.cs" Link="Core\Backend\BackendDtos.cs" />
```

**Step 4: GREEN 확인**

같은 filtered test를 다시 실행한다. 예상: PASS.

### Task 2: 실행파일 경로 JSON 기반 백엔드 설정 추가

**파일:**
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IBackendSettingsPathProvider.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendSettingsPathProvider.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/ISettingsService.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/SettingsService.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsSourceTests.cs`

**Step 1: 파일 roundtrip 실패 테스트 작성**

소스 문자열 검사가 아니라 실제 임시 파일에 저장하고 새 `SettingsService` 인스턴스로 다시 로드해 핵심 요구사항을 검증한다. 테스트는 실제 실행파일 폴더를 건드리지 않도록 fake `IBackendSettingsPathProvider`를 사용한다.

```csharp
private sealed class FakeBackendSettingsPathProvider : IBackendSettingsPathProvider
{
    public FakeBackendSettingsPathProvider(string filePath)
    {
        BackendSettingsFilePath = filePath;
    }

    public string BackendSettingsFilePath { get; }
}

private static string CreateTempSettingsPath()
{
    string directory = Path.Combine(Path.GetTempPath(), "ui_winui3_backend_settings_tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    return Path.Combine(directory, "backend-settings.json");
}

[TestMethod]
public void BackendSettings_LoadsDefaultsWhenFileIsMissing()
{
    var settings = new SettingsService(new FakeBackendSettingsPathProvider(CreateTempSettingsPath()));

    settings.Load();

    Assert.IsTrue(settings.IsBackendSyncEnabled);
    Assert.AreEqual("pc_name_001", settings.BackendPcName);
    Assert.AreEqual("1.1.1.0", settings.BackendSoftwareVersion);
    Assert.AreEqual(0, settings.BackendDeviceObjectMappings.Count);
}

[TestMethod]
public void BackendSettings_SaveAndLoad_RoundTripsValues()
{
    string path = CreateTempSettingsPath();
    var first = new SettingsService(new FakeBackendSettingsPathProvider(path))
    {
        IsBackendSyncEnabled = false,
        BackendBaseUrl = "https://example.invalid",
        BackendStoreId = "store-1",
        BackendPcId = "pc-1",
        BackendPcName = "Main PC",
        BackendSoftwareVersion = "1.2.3.4",
        BackendDeviceObjectMappings = new Dictionary<int, string> { [2] = "obj-1" },
    };

    first.Save();

    var second = new SettingsService(new FakeBackendSettingsPathProvider(path));
    second.Load();

    Assert.IsFalse(second.IsBackendSyncEnabled);
    Assert.AreEqual("https://example.invalid", second.BackendBaseUrl);
    Assert.AreEqual("store-1", second.BackendStoreId);
    Assert.AreEqual("pc-1", second.BackendPcId);
    Assert.AreEqual("Main PC", second.BackendPcName);
    Assert.AreEqual("1.2.3.4", second.BackendSoftwareVersion);
    Assert.AreEqual("obj-1", second.BackendDeviceObjectMappings[2]);
}

[TestMethod]
public void BackendSettings_InvalidJson_FallsBackToDefaults()
{
    string path = CreateTempSettingsPath();
    File.WriteAllText(path, "{bad json");

    var settings = new SettingsService(new FakeBackendSettingsPathProvider(path));
    settings.Load();

    Assert.IsTrue(settings.IsBackendSyncEnabled);
    Assert.AreEqual("pc_name_001", settings.BackendPcName);
    Assert.AreEqual("1.1.1.0", settings.BackendSoftwareVersion);
}
```

**Step 2: RED 확인**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.Tests\AnimatronicsControlCenter.Tests.csproj --filter BackendSettings
```

예상: `IBackendSettingsPathProvider`, 백엔드 설정 property, 파일 저장/로드 구현이 없어서 실패.

**Step 3: 설정 구현**

백엔드 설정은 `ApplicationData.Current.LocalSettings`에 섞지 않고 실행파일 경로의 `backend-settings.json`에 저장한다.

구현 방향:

- `SettingsService`에 백엔드 설정 property를 추가한다.
- `SettingsService` 생성자는 `IBackendSettingsPathProvider`를 받는다.
- 운영용 `BackendSettingsPathProvider`는 `Path.Combine(AppContext.BaseDirectory, "backend-settings.json")`을 반환한다.
- 테스트는 fake provider로 임시 파일 경로를 주입한다.
- `IsBackendSyncEnabled`의 기본값은 `true`로 둔다.
- `Save()`는 기존 일반 설정은 계속 `ApplicationData.Current.LocalSettings`에 저장한다.
- 백엔드 설정은 별도 private method `SaveBackendSettingsFile()`에서 JSON 파일로 저장한다.
- `Load()`는 기존 일반 설정을 로드한 뒤 `LoadBackendSettingsFile()`로 백엔드 설정 파일을 읽는다.
- 파일이 없으면 기본값을 사용한다.
- 파일에 `isBackendSyncEnabled`가 있으면 그 값을 그대로 사용해 활성/비활성 상태를 유지한다.
- 설정 파일 JSON이 깨져 있으면 기본값을 사용하고 앱이 죽지 않게 한다.
- `backendDeviceObjectMappings`는 파일에서 `Dictionary<int,string>`로 역직렬화한다. 키가 문자열 숫자(`"2"`)로 저장되어도 로드 후 앱 내부에서는 `int` 키로 다룬다.
- ViewModel의 textarea 편집용 JSON 문자열은 저장 버튼에서만 파싱한다. 파싱 실패 시 `SettingsService`에 반영하지 않고 사용자에게 오류를 표시한다.

제안 JSON 형식:

```json
{
  "isBackendSyncEnabled": true,
  "backendBaseUrl": "https://robot-monitor-api-dev.innergm.com",
  "backendBearerToken": "",
  "backendStoreId": "",
  "backendStoreName": "",
  "backendStoreCountryCode": "",
  "backendPcId": "",
  "backendPcName": "pc_name_001",
  "backendSoftwareVersion": "1.1.1.0",
  "backendDeviceObjectMappings": {
    "2": "obj-1"
  },
  "backendSyncIntervalSeconds": 5
}
```

**Step 4: GREEN 확인**

filtered test 재실행. 예상: PASS.

### Task 3: Object ID Resolver 추가

**파일:**
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendObjectIdResolver.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendObjectIdResolverTests.cs`
- 수정: 테스트 프로젝트 compile include

**Step 1: 실패 테스트 작성**

정상 매핑과 누락 매핑을 테스트한다. Resolver는 JSON 파싱 책임을 갖지 않고, `SettingsService.BackendDeviceObjectMappings` 딕셔너리만 읽는다.

```csharp
[TestMethod]
public void ResolveObjectId_ReturnsMappedObjectId()
{
    var settings = new FakeSettingsService
    {
        BackendDeviceObjectMappings = new Dictionary<int, string>
        {
            [2] = "obj-1"
        }
    };
    var resolver = new BackendObjectIdResolver(settings);

    Assert.AreEqual("obj-1", resolver.ResolveObjectId(2));
}

[TestMethod]
public void ResolveObjectId_ReturnsNullForMissingMapping()
{
    var settings = new FakeSettingsService
    {
        BackendDeviceObjectMappings = new Dictionary<int, string>()
    };
    var resolver = new BackendObjectIdResolver(settings);

    Assert.IsNull(resolver.ResolveObjectId(2));
}
```

**Step 2: RED 확인**

예상: resolver가 없어서 실패.

**Step 3: Resolver 구현**

`settings.BackendDeviceObjectMappings.TryGetValue(deviceId, out var objectId)`로 바로 조회한다. JSON 파싱은 Resolver에서 하지 않는다.

**Step 4: GREEN 확인**

resolver 테스트 실행.

### Task 4: HTTP 백엔드 조회/송신 클라이언트 추가

**파일:**
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendMonitoringService.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendServerCatalogClient.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendMonitoringServiceTests.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendServerCatalogClientTests.cs`
- 수정: 테스트 프로젝트 compile include

**Step 1: fake HttpMessageHandler 기반 실패 테스트 작성**

검증 항목:

- `GetStoreDetailAsync("store-1")`이 `GET /v1/service/stores/store-1/detail`을 호출한다.
- `IsBackendSyncEnabled == false`여도 `GetStoreDetailAsync`는 호출된다.
- store detail 응답의 `store_id`, `store_name`, `country_code`, `pcs[*].pc_id`, `pcs[*].sw_version`, `pcs[*].objects[*].id`를 DTO로 역직렬화한다.
- `UpdatePcMetadataAsync("store-1", "pc-1", request)`가 `PUT /v1/service/stores/store-1/pcs/pc-1`을 호출한다.
- `IsBackendSyncEnabled == false`여도 `UpdatePcMetadataAsync`는 호출된다.
- PC metadata update request body에 `pc_name`과 `sw_version`이 포함된다.
- `SendObjectLogAsync(device)`가 `POST /v1/service/objects/{object_id}/logs`를 보낸다.
- `IsBackendSyncEnabled == false`인 자동 동기화 경로에서는 `SendObjectLogAsync`를 호출하지 않거나, 호출되더라도 네트워크 요청 없이 `"Backend sync disabled."`를 반환한다.
- log request body에 `power_status`, `operation_status`, `error_data`가 포함된다.
- `/v1/service/objects/{object_id}/power`는 호출하지 않는다.
- token이 있으면 `Authorization: Bearer ...`가 포함된다.
- token이 비어 있으면 Authorization 헤더를 보내지 않는다.
- `HttpClient.BaseAddress`를 설정하지 않고, 요청마다 `BackendBaseUrl`에서 absolute URI를 만들어 전송한다.
- 동적 token은 `HttpClient.DefaultRequestHeaders`에 넣지 않고, 요청마다 `HttpRequestMessage.Headers.Authorization`에 설정한다.
- non-2xx 응답은 `BackendSendResult(false, status, message)`로 반환한다.

**Step 2: RED 확인**

예상: 조회/송신 service가 없어서 실패.

**Step 3: 최소 구현**

생성자:

```csharp
public BackendMonitoringService(
    HttpClient httpClient,
    ISettingsService settingsService,
    IBackendObjectIdResolver objectIdResolver)
```

규칙:

- `BackendServerCatalogClient`는 `IBackendServerCatalogClient`를 구현하고 store detail 조회와 기존 PC metadata 갱신을 담당한다.
- PC metadata 갱신은 `PUT /v1/service/stores/{store_id}/pcs/{pc_id}`로 수행하고, payload는 `{ "pc_name": "...", "sw_version": "..." }`이다.
- 이 갱신은 기존 PC에 대한 보고이며, Store/PC/Object 생성 API를 호출하지 않는다.
- `BackendServerCatalogClient.GetStoreDetailAsync`와 `UpdatePcMetadataAsync`는 수동 설정 화면 동작이므로 `IsBackendSyncEnabled`가 false여도 호출할 수 있다.
- `BackendMonitoringService.SendObjectLogAsync`는 자동 background sync 송신용이다. 정상 실행 경로에서는 `BackendDashboardSyncService`가 `IsBackendSyncEnabled`를 확인한 뒤에만 호출한다.
- 방어적으로 `BackendMonitoringService.SendObjectLogAsync` 내부에서도 `IsBackendSyncEnabled == false`이면 `"Backend sync disabled."` 결과를 반환해도 된다. 단, 이 규칙을 `BackendServerCatalogClient`에는 적용하지 않는다.
- `HttpClient`는 singleton으로 재사용하되 `BaseAddress`를 설정하거나 요청 중 변경하지 않는다.
- `BackendBaseUrl`은 요청 시점마다 `ISettingsService`에서 읽고, endpoint path와 결합해 absolute URI를 만든다.
- token이 비어 있지 않으면 `HttpRequestMessage`마다 Authorization 헤더를 추가한다.
- `HttpClient.DefaultRequestHeaders.Authorization`에는 동적 token을 저장하지 않는다.
- JSON은 `JsonSerializerDefaults.Web` 기준으로 직렬화.
- DI 등록 시 timeout은 15초로 설정.
- `SendObjectLogAsync(Device device, ...)`는 `IBackendObjectIdResolver.ResolveObjectId(device.Id)` 결과가 없으면 네트워크 호출 없이 실패 결과를 반환한다.
- `SendObjectLogAsync`는 `BackendDeviceMapper.CreateObjectLog(device)` 결과를 `/v1/service/objects/{object_id}/logs`로 보낸다.
- `BackendMonitoringService`에는 전원 제어 메서드를 만들지 않는다. 이 프로그램은 백엔드에서 전원 명령을 받는 주체이며, 백엔드에 전원 제어 명령을 보내지 않는다.

**Step 4: GREEN 확인**

조회/송신 service 테스트 실행.

### Task 5: 서버/로컬 설정 비교 모델 추가

**파일:**
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Backend/BackendSettingsComparison.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Backend/BackendServerSnapshot.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsComparisonTests.cs`
- 수정: 테스트 프로젝트 compile include

**Step 1: 실패 테스트 작성**

비교 결과가 필드별로 같은지/다른지와 이유를 제공하는지 테스트한다.

`BackendServerSnapshot`은 Store 전체 PC 목록이 아니라, 서버 Store detail 응답에서 현재 `BackendPcId`와 일치하는 PC 1개를 선택해 만든 현재 프로그램 전용 snapshot이다.

```csharp
[TestMethod]
public void Compare_ReturnsFieldLevelMismatchReasons()
{
    var server = new BackendServerSnapshot(
        StoreId: "store-1",
        StoreName: "Seoul Store",
        StoreCountryCode: "KR",
        PcId: "pc-1",
        PcName: "Main PC",
        SwVersion: "1.1.1.0",
        Objects: new[] { new BackendServerObjectSnapshot("obj-1", "Robot A") });

    var local = new BackendLocalSettingsSnapshot(
        StoreId: "store-1",
        StoreName: "Seoul Store",
        StoreCountryCode: "JP",
        PcId: "pc-2",
        PcName: "pc_name_001",
        SwVersion: "1.1.2.0",
        DeviceObjectMappings: new Dictionary<int, string> { [2] = "obj-missing" });

    var result = BackendSettingsComparison.Compare(server, local);

    Assert.IsTrue(result.Fields.Single(x => x.FieldName == "StoreId").IsMatch);
    Assert.IsFalse(result.Fields.Single(x => x.FieldName == "StoreCountryCode").IsMatch);
    StringAssert.Contains(result.Fields.Single(x => x.FieldName == "StoreCountryCode").Message, "서버 값");
    StringAssert.Contains(result.Fields.Single(x => x.FieldName == "SwVersion").Message, "sw_version");
    StringAssert.Contains(result.Fields.Single(x => x.FieldName == "PcId").Message, "PC ID");
    StringAssert.Contains(result.Fields.Single(x => x.FieldName == "DeviceObjectMappings").Message, "Object ID");
}
```

**Step 2: RED 확인**

예상: 비교 모델과 비교 함수가 없어서 실패.

**Step 3: 비교 모델 구현**

제안 타입:

```csharp
public sealed record BackendServerSnapshot(
    string StoreId,
    string? StoreName,
    string? StoreCountryCode,
    string? PcId,
    string? PcName,
    string? SwVersion,
    IReadOnlyList<BackendServerObjectSnapshot> Objects);

public sealed record BackendServerObjectSnapshot(string ObjectId, string? ObjectName);

public sealed record BackendLocalSettingsSnapshot(
    string StoreId,
    string? StoreName,
    string? StoreCountryCode,
    string PcId,
    string PcName,
    string SwVersion,
    IReadOnlyDictionary<int, string> DeviceObjectMappings);

public sealed record BackendFieldComparison(
    string FieldName,
    string DisplayName,
    bool IsMatch,
    string Message);

public sealed record BackendSettingsComparisonResult(
    bool CanCompare,
    IReadOnlyList<BackendFieldComparison> Fields);
```

구현 규칙:

- Store detail 응답에 여러 PC가 있어도 이 프로그램은 설정된 `BackendPcId`와 일치하는 PC 1개만 선택한다.
- 선택된 PC로 `BackendServerSnapshot`을 만들며, 다른 PC는 다른 장비/프로그램 인스턴스의 소유로 보고 비교하지 않는다.
- Store detail 응답에서 `BackendPcId`와 일치하는 PC를 찾지 못하면 `CanCompare == false` 또는 `PcId` 필드 불일치로 표시하고, `서버 Store 아래에 이 PC ID가 없습니다.` 메시지를 반환한다.
- 서버 조회 결과가 없으면 `CanCompare == false`와 `서버 값을 먼저 조회해야 비교할 수 있습니다.` 메시지를 반환한다.
- 같은 값은 `IsMatch == true`, 빈 메시지 또는 중립 메시지로 둔다.
- 다른 값은 `IsMatch == false`이고 오른쪽 입력창 아래에 표시할 빨간 메시지를 포함한다.
- `PcId`는 선택된 현재 PC의 ID와 로컬 `BackendPcId`를 비교한다.
- `SwVersion`은 선택된 현재 PC의 `sw_version`과 로컬 `BackendSoftwareVersion`을 비교한다.
- 로컬 `SwVersion`은 이 PC에 설치된 `ui_winui3` 버전이며, 서버 값과 다르면 오른쪽 Software Version 입력 아래에 불일치 메시지를 표시한다.
- Object ID 매핑은 선택된 PC의 object 목록에 존재하는지 확인한다.

**Step 4: GREEN 확인**

comparison 테스트 실행.

### Task 6: 왼쪽 하단 지구본/서버 아이콘 내비게이션 추가

**파일:**
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/MainWindow.xaml`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/MainWindow.xaml.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsNavigationTests.cs`

**Step 1: 소스 검사 실패 테스트 작성**

검증 항목:

- `MainWindow.xaml`의 `NavigationView.PaneFooter`에 `BackendSettingsButton`이 있다.
- 버튼은 지구본/서버를 의미하는 `FontIcon`을 사용한다. 후보 glyph는 Segoe MDL2 Assets의 globe/web/server 계열이며, 구현 후 실제 표시를 확인한다.
- `MainWindow.xaml.cs`에 `BackendSettingsButton_Click`가 있고 `ContentFrame.Navigate(typeof(BackendSettingsPage))`를 호출한다.
- 기존 serial traffic footer 버튼은 유지한다.

**Step 2: RED 확인**

예상: 버튼과 페이지가 없어서 실패.

**Step 3: footer 구조 변경**

`NavigationView.PaneFooter` 안의 기존 `SerialTrafficFooterHost`를 세로 컨테이너로 감싼다.

구현 방향:

- `StackPanel Orientation="Vertical"` 또는 2행 `Grid` 사용.
- 아이콘 버튼 크기는 기존 footer 버튼과 맞춰 `48x48` 안에서 동작하게 한다.
- `ToolTipService.ToolTip="백엔드 설정"`을 지정한다.
- 클릭 즉시 페이지를 전환하고, 서버 조회나 파일 I/O 같은 느린 작업은 클릭 핸들러에서 하지 않는다.

**Step 4: 페이지 shell 생성**

`BackendSettingsPage.xaml`은 우선 빈 ViewModel 바인딩 가능한 shell만 만든다. 실제 입력 컨트롤은 Task 7에서 추가한다.
`BackendSettingsPage.xaml.cs`는 기존 `SettingsPage` 패턴을 따른다. Page는 `Frame.Navigate(typeof(BackendSettingsPage))`로 WinUI가 생성하고, 생성자에서 `App.Current.Services.GetRequiredService<BackendSettingsViewModel>()`로 ViewModel만 DI에서 가져온다.

```csharp
public sealed partial class BackendSettingsPage : Page
{
    public BackendSettingsViewModel ViewModel { get; }

    public BackendSettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<BackendSettingsViewModel>();
    }
}
```

**Step 5: GREEN 확인**

navigation 테스트 실행.

### Task 7: 2분할 백엔드 설정 화면 UI와 ViewModel 추가

**파일:**
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendSettingsViewModel.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsViewModelTests.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsPageXamlTests.cs`

**Step 1: ViewModel 실패 테스트 작성**

검증 항목:

- 생성 시 `backend-settings.json` 값을 로드하고, 파일이 없으면 기본값을 사용한다.
- `FetchServerCommand`는 `IBackendServerCatalogClient.GetStoreDetailAsync(StoreId)`를 호출하고 서버 snapshot을 만든다.
- Store detail 응답에 여러 PC가 있어도 `BackendPcId`와 일치하는 PC 1개만 snapshot으로 만든다.
- `BackendPcId`와 일치하는 PC가 없으면 다른 PC를 대신 선택하지 않고, 비교/적용 불가 메시지를 표시한다.
- `ApplyServerValuesCommand`는 서버 snapshot의 Store/PC/Object 값을 오른쪽 로컬 draft에 복사하지만 저장하지 않는다.
- `CompareWithServerCommand`는 비교 결과를 필드별 메시지로 노출한다.
- `SaveCommand`는 오른쪽 로컬 draft를 설정 파일에 저장한다.
- device/object mapping textarea는 ViewModel의 `BackendDeviceObjectMappingsText` 같은 임시 문자열 속성으로만 둔다.
- 저장 시 `BackendDeviceObjectMappingsText`를 `Dictionary<int,string>`로 파싱하고, 성공한 경우에만 `SettingsService.BackendDeviceObjectMappings`에 반영한다.
- mapping JSON 파싱에 실패하면 설정 파일 저장과 서버 보고를 중단하고 해당 입력 아래에 빨간 오류 메시지를 표시한다.
- `SaveCommand` 이후 기존 백엔드 PC에 `pc_name`과 `sw_version`을 보고한다. 서버 보고 실패 시 설정 파일 저장은 유지하고, 실패 메시지를 오른쪽 화면에 표시한다.

**Step 2: XAML 소스 검사 실패 테스트 작성**

검증 항목:

- 왼쪽 서버 영역, 가운데 `서버 값 적용` 버튼, 오른쪽 로컬 설정 영역이 3열 layout으로 존재한다.
- 왼쪽 영역에 `서버 조회` 버튼이 있다.
- 오른쪽 영역에 `저장` 버튼과 `서버 값 비교` 버튼이 있다.
- Store ID, Store Name, Store Country Code, PC ID, PC Name, Software Version, device/object mapping 입력이 있다.
- 각 입력 아래에 비교 메시지를 표시할 `TextBlock`이 있고, 불일치 메시지는 빨간색 스타일을 쓴다.

**Step 3: RED 확인**

예상: ViewModel과 XAML 컨트롤이 없어서 실패.

**Step 4: ViewModel 구현**

구현 규칙:

- page 진입은 즉시 끝나야 한다. 생성자에서 네트워크 호출을 하지 않는다.
- 서버 조회는 명시적으로 `서버 조회` 버튼을 눌렀을 때만 수행한다.
- 파일 저장은 `저장` 버튼에서만 수행한다.
- device/object mapping은 UI에서는 JSON 문자열로 보여주되, 저장 전에는 반드시 `Dictionary<int,string>`로 변환한다.
- 변환 실패 시 기존 설정 딕셔너리를 덮어쓰지 않는다.
- `저장` 후 `Store ID`와 `PC ID`가 있으면 `IBackendServerCatalogClient.UpdatePcMetadataAsync`로 `pc_name`과 `sw_version`을 보고한다.
- PC metadata 보고는 사용자가 비활성화한 background sync 상태와 별개로, 명시적인 저장 동작에 대한 수동 REST 호출로 취급한다.
- 서버 값 적용은 local draft만 바꾸며, 저장 여부는 사용자에게 맡긴다.
- 비교는 마지막 서버 조회 결과와 현재 local draft를 기준으로 한다.
- 서버 조회 실패 메시지는 왼쪽 서버 영역에 표시하고, 오른쪽 필드 오류와 섞지 않는다.

**Step 5: XAML 구현**

레이아웃:

- 최상위는 3열 `Grid`: 왼쪽 `*`, 가운데 `Auto`, 오른쪽 `*`.
- 페이지 섹션을 중첩 카드로 만들지 않는다. 입력 그룹은 full-width panel 또는 기존 Settings 스타일을 따른다.
- 가운데 버튼은 아이콘+텍스트 또는 짧은 텍스트 `서버 값 적용`으로 둔다.
- 빨간 비교 메시지는 오른쪽 입력 아래 `Foreground`를 error brush로 지정한다.

**Step 6: GREEN 확인**

ViewModel 테스트와 XAML 테스트 실행.

### Task 8: 펌웨어 전원 상태와 장치 상태를 백엔드 DTO로 변환

**파일:**
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Models/Device.cs`
- 수정: 전원 상태가 들어오는 프로토콜 파서/투영 파일
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Backend/BackendDeviceMapper.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendDeviceMapperTests.cs`

**Step 1: 실패 테스트 작성**

`MotionState` -> backend `operation_status` 매핑 테스트:

```csharp
[DataTestMethod]
[DataRow(MotionState.Playing, "PLAY")]
[DataRow(MotionState.Stopped, "STOP")]
[DataRow(MotionState.Idle, "STOP")]
[DataRow(MotionState.Paused, "STOP")]
public void MapOperationStatus_MapsMotionState(MotionState state, string expected)
```

펌웨어 전원 상태 -> `power_status` 매핑 테스트:

```csharp
[DataTestMethod]
[DataRow("ON", "ON")]
[DataRow("OFF", "OFF")]
[DataRow("", "OFF")]
public void MapPowerStatus_UsesFirmwarePowerStatus(string powerStatus, string expected)
```

모터 오류 -> `error_data` 매핑 테스트:

```csharp
[TestMethod]
public void CreateObjectLog_MapsMotorErrorsToErrorData()
{
    var device = new Device(2) { PowerStatus = "ON", MotionState = MotionState.Stopped };
    device.Motors.Add(new MotorState
    {
        Id = 2,
        GroupId = 1,
        SubId = 2,
        Type = "AC",
        Status = "Error"
    });

    var log = BackendDeviceMapper.CreateObjectLog(device);

    Assert.AreEqual("ON", log.PowerStatus);
    Assert.AreEqual("STOP", log.OperationStatus);
    Assert.AreEqual(1, log.ErrorData.Count);
    Assert.AreEqual("1-2", log.ErrorData[0].BoardId);
    Assert.AreEqual("AC", log.ErrorData[0].BoardType);
    Assert.AreEqual("Error", log.ErrorData[0].ErrorCode);
}
```

**Step 2: RED 확인**

예상: `PowerStatus`와 mapper가 없어서 실패.

**Step 3: Device 모델과 Mapper 구현**

`Device`에 `PowerStatus` 속성을 추가하고, `Device`에서 `BackendObjectLogRequest`를 만든다.

```csharp
public static BackendObjectLogRequest CreateObjectLog(Device device)
```

구현 규칙:

- `PowerStatus`는 `device.PowerStatus == "ON"`일 때만 `"ON"`, 그 외에는 `"OFF"`.
- `MotionState.Playing`은 `"PLAY"`, 나머지는 `"STOP"`.
- `MotorState.Status == "Normal"`인 모터는 error_data에서 제외.
- `Error`, `Overload`, `Disconnected` 모터는 `BackendErrorData`로 변환.
- `PowerConsumption`은 null로 둔다.

**Step 4: 펌웨어 전원 상태 투영 구현**

확정된 v1 wire format에 맞춰 기존 PONG payload 끝의 선택적 1 byte를 전원 상태로 파싱한다.

- `Core/Protocol/PingTimeSettings.cs` 또는 `PongStatus` 정의 위치
- `Core/Protocol/BinaryDeserializer.cs`
- `Core/Models/FirmwareStatusProjection.cs`

파싱 규칙:

- PONG payload가 기존 길이면 기존 방식대로 정상 파싱하고 `PowerStatus = "OFF"`를 유지한다.
- 추가 byte가 `0x01`이면 `PowerStatus = "ON"`으로 투영한다.
- 추가 byte가 `0x00`이면 `PowerStatus = "OFF"`로 투영한다.
- 알 수 없는 추가 byte 값은 예외 없이 `PowerStatus = "OFF"`로 투영한다.

**Step 5: GREEN 확인**

mapper 테스트 실행.

### Task 9: DI 등록

**파일:**
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/App.xaml.cs`

**Step 1: 소스 검사 실패 테스트 작성**

`App.xaml.cs`에 다음 등록이 있는지 검사한다.

```csharp
services.AddSingleton<IBackendObjectIdResolver, BackendObjectIdResolver>();
services.AddSingleton<IBackendSettingsPathProvider, BackendSettingsPathProvider>();
services.AddSingleton<IBackendMonitoringService, BackendMonitoringService>();
services.AddSingleton<IBackendServerCatalogClient, BackendServerCatalogClient>();
services.AddSingleton<IBackendDashboardSyncService, BackendDashboardSyncService>();
services.AddSingleton<HttpClient>();
services.AddTransient<BackendSettingsViewModel>();
```

실제 `HttpClient` 등록은 factory로 `Timeout = TimeSpan.FromSeconds(15)`를 지정해도 된다.
`HttpClient.BaseAddress`와 `DefaultRequestHeaders.Authorization`은 등록 시점에 설정하지 않는다. 서버 URL과 token은 사용자 설정에서 런타임에 바뀔 수 있으므로 요청마다 `HttpRequestMessage`에 적용한다.
`IBackendSettingsPathProvider`는 운영 환경에서 실행파일 경로의 `backend-settings.json`을 반환한다. 테스트에서는 fake provider로 임시 파일 경로를 주입한다.
`BackendSettingsPage`는 `Frame.Navigate(typeof(BackendSettingsPage))`로 WinUI가 직접 생성하므로 Page 자체는 DI에 등록하지 않는다. `BackendSettingsPage` 생성자가 `GetRequiredService<BackendSettingsViewModel>()`로 ViewModel을 가져오도록 `BackendSettingsViewModel`만 transient로 등록한다.

**Step 2: RED 확인**

예상: 등록이 없어서 실패.

**Step 3: 서비스 등록**

```csharp
services.AddSingleton<HttpClient>(_ => new HttpClient
{
    Timeout = TimeSpan.FromSeconds(15)
});
services.AddSingleton<IBackendObjectIdResolver, BackendObjectIdResolver>();
services.AddSingleton<IBackendSettingsPathProvider, BackendSettingsPathProvider>();
services.AddSingleton<IBackendMonitoringService, BackendMonitoringService>();
services.AddSingleton<IBackendServerCatalogClient, BackendServerCatalogClient>();
services.AddSingleton<IBackendDashboardSyncService, BackendDashboardSyncService>();
services.AddTransient<BackendSettingsViewModel>();
```

**Step 4: GREEN 확인**

DI 소스 검사 테스트 실행.

### Task 10: Dashboard 전역 백엔드 동기화 서비스 추가

**파일:**
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendDashboardSyncService.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/DashboardViewModel.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendDashboardSyncServiceTests.cs`

**Step 1: 실패 테스트 작성**

검증 항목:

- `BackendDashboardSyncService.ReplaceDevices(devices)`가 동기화 대상 장치 목록을 교체한다.
- `Start()` 후 interval tick에서 모든 장치에 대해 `ISerialService.PingDeviceAsync(device.Id)`를 호출한다.
- ping 결과가 있으면 해당 `Device` 상태를 갱신하고 `IBackendMonitoringService.SendObjectLogAsync(device, token)`를 호출한다.
- ping 실패 장치는 `PowerStatus = "OFF"` 로그를 송신하고, `error_data`에 `{ boardId: "<deviceId>", boardType: "DEVICE", errorCode: "Disconnected" }`를 포함한다.
- backend 실패가 sync loop를 중단하지 않는다.
- `Stop()`이 loop를 취소한다.

**Step 2: RED 확인**

예상: sync service가 없어서 실패.

**Step 3: Sync service 구현**

구현 규칙:

- `IsBackendSyncEnabled == false`면 loop를 돌리지 않는다.
- interval은 `BackendSyncIntervalSeconds`를 사용하고 최소 1초로 clamp한다.
- 장치 목록은 lock 또는 snapshot copy로 다룬다.
- 한 장치의 실패가 다른 장치 송신을 막지 않는다.
- serial polling과 backend POST는 UI thread를 막지 않는다.
- `CancellationTokenSource`로 Start/Stop을 제어한다.

**Step 4: DashboardViewModel 연결**

`DashboardViewModel` 생성자에 `IBackendDashboardSyncService`를 주입한다.

스캔 완료 후:

```csharp
_backendDashboardSyncService.ReplaceDevices(Devices);
_backendDashboardSyncService.Start();
```

장치 목록이 비거나 앱 종료 시 `Stop()`을 호출한다.

**Step 5: GREEN 확인**

sync service 테스트와 Dashboard 관련 테스트를 실행한다.

### Task 11: 실제 서버 수동 스모크 테스트 기록

**파일:**
- 생성: `docs/backend-rest-smoke-test.md`

**Step 1: 스모크 테스트 문서 작성**

다음 항목을 기록할 수 있는 체크리스트 문서를 만든다.

```markdown
# Backend REST Smoke Test

## Environment

- BackendBaseUrl:
- Store ID:
- PC ID:
- Object ID:
- Test date:

## Checks

- [ ] Store detail 조회 성공: `GET /v1/service/stores/{store_id}/detail`
- [ ] 현재 `BackendPcId`와 일치하는 PC가 응답에 존재함
- [ ] PC metadata 갱신 성공: `PUT /v1/service/stores/{store_id}/pcs/{pc_id}`
- [ ] Object log 송신 성공: `POST /v1/service/objects/{object_id}/logs`
- [ ] `/logs` 송신 후 store detail의 `power_status` 반영 여부 확인
- [ ] `/v1/service/objects/{object_id}/power`가 호출되지 않았음

## Result

- Passed:
- Notes:
```

**Step 2: 실제 서버 연결 전 안전 확인**

- 테스트용 Store/PC/Object ID를 사용한다.
- 운영 데이터에 영향이 있는지 모르면 실행하지 않고 문서에 `Blocked`로 기록한다.
- token이 필요한 환경이면 token은 문서에 저장하지 않는다.

**Step 3: 앱에서 수동 검증**

- Backend Settings 화면에서 서버 조회를 실행한다.
- 서버 값 비교가 현재 PC 기준으로 동작하는지 확인한다.
- 저장 후 PC metadata 보고 결과를 확인한다.
- Dashboard에 테스트 장치를 등록하고 `/logs` 송신이 성공하는지 확인한다.

**Step 4: 결과 기록**

`docs/backend-rest-smoke-test.md`에 성공/실패와 서버 반영 여부를 기록한다. 서버가 `/logs`는 받지만 detail의 `power_status`를 갱신하지 않는다면, 앱 구현 실패가 아니라 서버 계약 확인 필요 항목으로 기록한다.

## 검증 명령

구현 완료 후 실행:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.Tests\AnimatronicsControlCenter.Tests.csproj
dotnet build .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln -p:Platform=x64
```

예상:

- 모든 MSTest 통과.
- x64 solution build 오류 0개.

## 완료 기준

- 백엔드 동기화는 기본 활성화이며, 활성/비활성 상태는 `backend-settings.json`에 저장되고 다음 프로그램 실행 시 그대로 로드된다.
- 백엔드 설정 저장/로드는 `IBackendSettingsPathProvider`로 경로를 주입받아 구현하며, 테스트는 임시 파일 roundtrip으로 기본값/저장 후 재로드/깨진 JSON fallback을 검증한다.
- 사용자가 백엔드 동기화를 비활성화한 경우에는 Dashboard 자동 상태 송신 loop가 `/logs` 네트워크 호출을 하지 않는다.
- 백엔드 설정 화면의 `서버 조회`, `서버 값 비교`, `저장 후 PC metadata 보고`는 수동 REST 호출이므로 동기화 비활성화 상태에서도 동작한다.
- 백엔드 설정은 왼쪽 하단 지구본/서버 아이콘으로 여는 전용 화면에서 편집할 수 있고, 프로그램 실행파일 경로의 `backend-settings.json`에 저장된다.
- `BackendSettingsPage`는 기존 페이지 패턴처럼 ViewModel만 DI에서 가져오며, `BackendSettingsViewModel`은 `App.xaml.cs`에 transient로 등록된다.
- 전용 화면은 서버 조회 영역과 로컬 설정 영역을 2분할로 보여주며, `서버 값 적용` 버튼으로 조회 값을 로컬 draft에 복사할 수 있다.
- `서버 값 비교` 버튼은 서버 값과 로컬 설정의 같은/다른 값을 비교하고, 다른 값의 이유를 오른쪽 입력창 아래 빨간 메시지로 표시한다.
- 백엔드 설정 화면 진입 시 네트워크 조회나 USB/COM 포트 조회를 수행하지 않아 화면 전환을 지연시키지 않는다.
- 백엔드 REST 호출은 singleton `HttpClient`를 재사용하되, `BaseAddress`와 `DefaultRequestHeaders.Authorization`를 사용하지 않는다. 모든 요청은 현재 설정의 `BackendBaseUrl`로 absolute URI를 만들고, token은 요청별 header로 설정한다.
- 장치/Object ID 매핑은 설정 파일과 `SettingsService` 내부에서 `Dictionary<int,string>`로 유지한다. JSON 문자열은 백엔드 설정 화면의 textarea 편집용 임시 값으로만 사용한다.
- REST 요청 JSON 필드명이 백엔드 계약과 정확히 일치한다: `power_status`, `operation_status`, `error_data` 등.
- 전원 상태 보고는 `POST /v1/service/objects/{object_id}/logs`의 `power_status` 필드로 보낸다.
- 상태 로그는 `POST /v1/service/objects/{object_id}/logs`로 보낸다.
- `POST /v1/service/objects/{object_id}/power`는 전원 제어 API이므로 호출하지 않는다.
- Store/PC/Object 자동 생성 API는 호출하지 않는다.
- Dashboard에 스캔된 모든 장치가 전역 동기화 대상이다.
- Object ID 매핑이 없어도 device polling이 실패하지 않는다.
- `Paused`는 `"STOP"`으로 송신된다.
- `pc_name` 기본값은 `"pc_name_001"`, `sw_version` 기본값은 `"1.1.1.0"`이며 전용 백엔드 설정 화면에서 수정 가능하다.
- `sw_version`은 이 PC에 설치된 `ui_winui3`의 버전으로 취급하며, `PUT /v1/service/stores/{store_id}/pcs/{pc_id}`를 통해 기존 PC metadata에 보고된다.
- `error_data`는 모터 오류 상태를 `{ boardId, boardType, errorCode }`로 변환하고, `errorCode`는 문자열로 보낸다.
- PONG payload 끝의 선택적 1 byte 전원 상태 필드를 `Device.PowerStatus`로 투영한다. `0x01`은 `"ON"`, `0x00` 또는 누락/알 수 없는 값은 `"OFF"`로 처리한다.
- ping 실패 시 해당 오브제에 `"OFF"` 로그를 보낸다.
- ping 실패 로그에는 실제 전원 OFF와 구분할 수 있도록 `error_data`에 `"Disconnected"`를 함께 보낸다.
- 백엔드 오류가 serial/XBee 장치 통신을 깨지 않는다.
- Task 11의 실제 서버 스모크 테스트 문서를 작성하고, `/logs` 송신 후 detail 응답의 `power_status` 반영 여부를 기록한다.
- reference repository는 계속 ignore 상태이고 빌드에 포함되지 않는다.

## 구현 후 확인 항목

- 실제 서버에서 `/logs` 송신이 성공하지만 detail 응답의 `power_status`가 갱신되지 않으면 백엔드 계약 확인 이슈로 분리한다.
- 테스트용 Store/PC/Object ID와 token이 준비되지 않으면 Task 11은 `Blocked`로 기록하고, 앱 구현 완료 여부는 자동화 테스트와 build 결과로 판단한다.
