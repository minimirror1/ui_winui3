# 영업시간 장치 동기화 구현 계획

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**목표:** 서버에서 선택된 매장의 영업시간을 가져오고, WinUI3에 로컬 캐시로 저장한 뒤, 설정된 장치 ID 범위에 영업시간을 전송하고 다시 읽어서 동기화 상태를 확인한다.

**구조:** `PING`은 지금처럼 RTC 시간 보정 전용으로 둔다. 영업시간은 별도 동기화 흐름으로 분리한다. 서버 매장 상세 조회 → 로컬 캐시 저장 → 장치 전송 → 장치 저장값 읽기 → 서버/캐시 값과 비교 순서로 동작한다.

**기술:** .NET 8, WinUI 3, CommunityToolkit.Mvvm, MSTest, 기존 binary serial protocol, 기존 backend REST client.

## 구현 전에 확정된 내용과 남은 질문

1. **장치 전송 방식**

   영업시간은 새 바이너리 명령으로 보낸다.

   사용할 명령:

   - `SET_OPERATE_TIME`: 장치에 영업시간 저장
   - `GET_OPERATE_TIME`: 장치에 저장된 영업시간 읽기

   장점은 ACK, checksum, readback 검증이 명확하다는 점이다.

2. **서버 응답 형태**

   현재 실제 매장 상세 응답은 아래 형태다. 이 응답을 기준으로 DTO와 변환 로직을 맞춘다.

   ```json
   {
     "id": "0PK85FSNX2TFA",
     "store_name": "젠틀몬스터 스타필드 하남",
     "country_code": "KR",
     "address": "경기 하남시 미사대로 750 스타필드 하남 1F",
     "latitude": 37.545424,
     "longitude": 127.224058,
     "timezone": "Asia/Seoul",
     "operate_times": [
       { "day_of_week": "FRI", "open_time": "09:00", "close_time": "18:00" },
       { "day_of_week": "MON", "open_time": "09:00", "close_time": "18:00" },
       { "day_of_week": "SAT", "open_time": "09:00", "close_time": "18:00" },
       { "day_of_week": "SUN", "open_time": "09:00", "close_time": "18:00" },
       { "day_of_week": "THU", "open_time": "09:00", "close_time": "18:00" },
       { "day_of_week": "TUE", "open_time": "09:00", "close_time": "18:00" },
       { "day_of_week": "WED", "open_time": "09:00", "close_time": "18:00" }
     ],
     "created_at": "2026-02-25T01:49:43.727Z",
     "modified_at": "2026-02-25T01:49:43.727Z"
   }
   ```

   활용 방식:

   - `id`: 선택된 store id와 비교하고 캐시에도 저장한다.
   - `timezone`: 화면 표시와 검증용으로 저장한다.
   - `operate_times`: 장치에 전송할 7일 스케줄 원본이다.
   - `modified_at`: schedule version 역할로 사용한다.
   - `operate_times` 배열 순서는 보장하지 않는다. WinUI3 내부 모델에서는 항상 `MON`~`SUN` 순서로 정렬한다.
   - checksum은 `timezone`, `modified_at`, 정렬된 7일 영업시간으로 만든다.

3. **휴무일 표현**

   현재 응답에는 `is_closed`가 없다. `00:00` ~ `00:00`은 24시간 영업이 아니라 **휴무**로 정의한다.

4. **PING 시간 기준**

   장치 RTC는 매장 영업시간 판단에 쓰인다. 다만 timezone은 서버 값으로 자동 변경하지 않고, 기존 UI 설정 화면의 타임존 설정값을 사용한다.

   운영 방식:

   - 초기 세팅 시 PC 관리자가 WinUI3 설정 화면에서 `PingCountryCode`, `PingUtcOffsetMinutes`를 설정한다.
   - `PING` payload는 지금처럼 이 설정값 기준으로 만든다.
   - 영업시간 전송 payload의 `timezone_offset_min`도 같은 설정값 `PingUtcOffsetMinutes`를 사용한다.
   - 서버의 `timezone`은 화면에 표시하고, 설정값과 명백히 다르면 경고 메시지만 보여준다.

5. **1시간마다 전송의 의미**

   요구사항은 “정각마다”이므로 앱 시작 후 60분 간격이 아니라 다음 정각, 예를 들어 `17:00:00`, `18:00:00`, `19:00:00`에 전송한다.

## 확정 가정

- 영업시간이 자정을 넘는 경우는 없다.
- 장치는 받은 영업시간을 micro SD에 저장한다.
- 장치에는 RTC가 장착되어 있다.
- 수동 전송 화면의 기본 장치 범위는 설정 화면의 스캔 범위 `ScanStartId` ~ `ScanEndId`를 사용한다.
- 수동 전송 화면에서는 전송 범위를 사용자가 수정할 수 있다.
- 서버 장애 시 마지막으로 정상 저장된 로컬 캐시 파일을 사용한다.
- 캐시는 기존 설정 파일에 섞지 않고 별도 JSON 파일로 저장하는 쪽을 기본안으로 한다.
- 설정 버튼 위에 시계 아이콘 버튼을 만들고, 이 버튼을 영업시간 스케줄 화면 전환 버튼으로 사용한다.
- 영업시간 전송에는 기존 `SaveFile` / `GetFile` 파일 저장 방식을 쓰지 않는다.

## 권장 장치 데이터 구조

새 바이너리 명령을 쓴다면 payload는 고정 길이 구조가 좋다. firmware에서 파싱이 단순하다.

```text
SET_OPERATE_TIME 요청 payload
  format_version        uint8   // 1
  timezone_offset_min   int16   // 한국이면 540
  schedule_checksum     uint32
  day_count             uint8   // 7
  rows[7]

요일 row
  day_of_week           uint8   // 1=MON ... 7=SUN
  is_closed             uint8   // 0=영업, 1=휴무
  open_minutes          uint16  // 00:00부터 지난 분
  close_minutes         uint16

SET_OPERATE_TIME 응답 payload
  schedule_checksum     uint32

GET_OPERATE_TIME 응답 payload
  SET_OPERATE_TIME 요청 payload와 같은 구조
```

로컬 캐시 파일은 장치 전송용 파일이 아니라 WinUI3의 서버 장애 대비용이다. 캐시는 JSON으로 저장하되 장치에는 위 바이너리 payload를 전송한다.

## 작업 1: 서버 DTO와 영업시간 모델 추가

**파일:**

- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Backend/BackendDtos.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/OperatingHours/OperatingHoursSchedule.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursScheduleTests.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj`

**할 일:**

1. `BackendStoreDetailResponse`가 실제 응답의 `id`, `operate_times`, `created_at`, `modified_at`을 읽도록 수정한다.
   - 현재 DTO는 `store_id`를 기대하므로 실제 응답의 `id`를 받을 수 있게 해야 한다.
   - 기존 코드가 `StoreId` 이름을 쓰고 있으므로 C# 속성명은 가능하면 `StoreId`로 유지한다.
2. WinUI3 내부에서 쓸 `OperatingHoursSchedule`, `OperatingHoursDay` 모델을 만든다.
3. 서버 DTO를 내부 모델로 변환하는 로직을 만든다.
4. `00:00` ~ `00:00`은 휴무로 처리한다.
5. `operate_times` 배열 순서와 상관없이 내부 모델은 `MON`~`SUN` 순서로 정렬한다.
6. `timezone` + `modified_at` + 정렬된 7일 영업시간 기준으로 checksum을 만든다.
7. `modified_at`이 비어 있으면 `timezone` + 정렬된 7일 영업시간만으로 checksum을 만든다.

**테스트:**

- 실제 서버 예시 JSON에서 `id`, `timezone`, `operate_times`, `modified_at`이 역직렬화되는지 확인
- 7개 요일이 만들어지는지 확인
- 서버 배열 순서가 섞여 있어도 내부 모델은 `MON`~`SUN` 순서인지 확인
- 휴무일 처리가 맞는지 확인
- 같은 데이터는 항상 같은 checksum이 나오는지 확인

실행:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter OperatingHoursScheduleTests
```

## 작업 2: 로컬 캐시 파일 추가

**파일:**

- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IOperatingHoursCache.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/OperatingHoursCache.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/App.xaml.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursCacheTests.cs`

**할 일:**

1. 영업시간 캐시 파일을 별도로 만든다.
2. 파일명은 `operating-hours-cache.json`으로 한다.
3. 기존 backend settings 파일과 같은 앱 데이터 폴더에 저장한다.
4. 서버 장애 시 이 캐시를 읽을 수 있게 한다.
5. 깨진 파일이나 없는 파일은 예외 없이 `null`로 처리한다.

기존 `app-settings.json`에 섞지 않는 이유는 영업시간이 사용자 설정이 아니라 서버에서 내려온 운영 데이터이기 때문이다.

**테스트:**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter OperatingHoursCacheTests
```

## 작업 3: 서버에서 영업시간 가져오는 서비스

**파일:**

- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IOperatingHoursSource.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/OperatingHoursSource.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/App.xaml.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursSourceTests.cs`

**할 일:**

1. `ISettingsService.BackendStoreId`로 선택된 매장 ID를 읽는다.
2. `IBackendServerCatalogClient.GetStoreDetailAsync`로 매장 상세를 가져온다.
3. 응답의 `operate_times`를 `OperatingHoursSchedule`로 변환한다.
4. 성공하면 캐시 파일에 저장한다.
5. 서버 실패 시 캐시를 읽어서 반환한다.
6. 서버도 실패하고 캐시도 없으면 실패 메시지를 반환한다.

**테스트:**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter OperatingHoursSourceTests
```

## 작업 4: 장치 프로토콜 추가

**파일:**

- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryProtocol.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinarySerializer.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryDeserializer.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursProtocolTests.cs`

**할 일:**

1. firmware와 명령 ID를 확정한다.

   제안:

   ```csharp
   SetOperateTime = 0x30,
   GetOperateTime = 0x31,
   ```

2. `SET_OPERATE_TIME` 요청 패킷 serializer를 만든다.
3. `GET_OPERATE_TIME` 요청 패킷 serializer를 만든다.
4. `SET_OPERATE_TIME` 응답 checksum parser를 만든다.
5. `GET_OPERATE_TIME` 응답 schedule parser를 만든다.
6. `timezone_offset_min`은 서버 timezone 변환값이 아니라 설정 화면의 `PingUtcOffsetMinutes`를 넣는다.

**테스트:**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter OperatingHoursProtocolTests
```

## 작업 5: 장치 전송/읽기 서비스

**파일:**

- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/ISerialService.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/SerialService.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/VirtualDeviceManager.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IOperatingHoursDeviceSyncService.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/OperatingHoursDeviceSyncService.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/App.xaml.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursDeviceSyncServiceTests.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/VirtualDeviceManagerOperatingHoursTests.cs`

**할 일:**

1. `ISerialService`에 영업시간 전송/읽기 메서드를 추가한다.
  ```csharp
   Task<OperatingHoursDeviceWriteResult> SetOperatingHoursAsync(int deviceId, OperatingHoursSchedule schedule);
   Task<OperatingHoursDeviceReadResult> GetOperatingHoursAsync(int deviceId);
  ```
2. 여러 장치에 순차 전송하는 `IOperatingHoursDeviceSyncService`를 만든다.
3. 전송 범위는 start id ~ end id inclusive로 처리한다.
4. 한 장치가 실패해도 나머지 장치 전송은 계속한다.
5. `VirtualDeviceManager`도 영업시간 저장/읽기를 지원하게 만든다.

**테스트:**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter "OperatingHoursDeviceSyncServiceTests|VirtualDeviceManagerOperatingHoursTests"
```

## 작업 6: 설정 화면 timezone 값을 영업시간 전송에 사용

**파일:**

- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/PingTimeSettings.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/OperatingHoursDeviceSyncService.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/OperatingHoursSyncViewModel.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/PingTimeSettingsTests.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursDeviceSyncServiceTests.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursSyncViewModelTests.cs`

**할 일:**

1. `PING` 시간 생성은 기존 방식 그대로 유지한다.
2. 영업시간 전송 payload의 `timezone_offset_min`은 `ISettingsService.PingUtcOffsetMinutes`를 사용한다.
3. 화면에는 서버 `timezone`과 현재 설정 offset을 같이 표시한다.
4. 서버 `timezone`이 `Asia/Seoul`인데 설정 offset이 `540`이 아니면 경고 메시지를 보여준다.
5. 이 작업에서는 서버 timezone으로 `PingCountryCode`, `PingUtcOffsetMinutes`를 자동 변경하지 않는다.

**테스트:**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter "PingTimeSettingsTests|OperatingHoursDeviceSyncServiceTests|OperatingHoursSyncViewModelTests"
```

## 작업 7: 수동 전송 화면 ViewModel

**파일:**

- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/OperatingHoursSyncViewModel.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursSyncViewModelTests.cs`

**할 일:**

1. 화면 진입 시 전송 범위를 설정의 스캔 범위로 초기화한다.
2. 서버/캐시에서 영업시간을 불러오는 명령을 만든다.
3. 사용자가 수정한 장치 ID 범위로 수동 전송하는 명령을 만든다.
4. 장치에 저장된 영업시간을 읽어오는 명령을 만든다.
5. 서버/캐시 checksum과 장치 checksum을 비교해서 상태를 표시한다.

표시 상태:

- `Synced`: 일치
- `Mismatch`: 불일치
- `Failed`: 전송/읽기 실패
- `NotSent`: 아직 전송 안 함

**테스트:**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter OperatingHoursSyncViewModelTests
```

## 작업 8: 수동 전송 화면 UI와 메뉴 추가

**파일:**

- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/OperatingHoursSyncPage.xaml`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/OperatingHoursSyncPage.xaml.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/App.xaml.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/MainWindow.xaml`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/MainWindow.xaml.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursSyncPageXamlTests.cs`

**화면 구성:**

- 상단 버튼: 서버에서 새로고침, 장치로 전송, 장치에서 읽기/비교
- 영업시간 표: 월~일, 휴무 여부, 시작 시간, 종료 시간
- 매장 정보 표시: 매장명, store id, 서버 timezone, `modified_at`, 현재 설정 timezone offset
- 전송 범위 입력: 시작 장치 ID, 종료 장치 ID
- 결과 표: 장치 ID, 전송 상태, 읽기 상태, checksum, 메시지
- 메인 좌측 footer의 기존 설정 버튼 위에 시계 아이콘 버튼을 추가한다.
- 시계 아이콘 버튼 클릭 시 `OperatingHoursSyncPage`로 이동한다.
- 현재 `MainWindow.xaml`에는 `BackendSettingsButton`이 footer 하단에 있으므로, 새 버튼은 그 바로 위에 배치한다.
- 아이콘은 WinUI 내장 `FontIcon`을 사용한다. 예: `Glyph="&#xE121;"`.

**테스트:**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter OperatingHoursSyncPageXamlTests
```

## 작업 9: 앱 시작 시 1회, 이후 정각마다 자동 전송

**파일:**

- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IOperatingHoursAutoSyncService.cs`
- 생성: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/OperatingHoursAutoSyncService.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/App.xaml.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/MainWindow.xaml.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursAutoSyncServiceTests.cs`

**동작:**

1. 앱 시작 후 영업시간을 서버에서 가져온다.
2. 실패하면 로컬 캐시를 사용한다.
3. 설정의 스캔 범위 장치들에 1회 전송한다.
4. 다음 정각까지 대기한다.
5. 이후 매 정각마다 같은 방식으로 전송한다.
6. 시리얼 연결이 끊겨 있으면 실패로 기록하고 다음 정각에 다시 시도한다.

**테스트:**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter OperatingHoursAutoSyncServiceTests
```

## 작업 10: 전체 검증

**전체 테스트:**

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln
```

**빌드:**

```powershell
dotnet build .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln
```

**수동 확인:**

1. 가상 장치 모드로 앱 실행
2. backend 설정에서 매장 선택
3. 좌측 footer의 시계 아이콘 버튼으로 영업시간 동기화 화면 열기
4. 전송 범위가 설정의 스캔 범위로 들어오는지 확인
5. 매장명, 서버 timezone, `modified_at`, 현재 설정 offset이 표시되는지 확인
6. 서버에서 새로고침 클릭
7. 장치로 전송 클릭
8. 장치에서 읽기/비교 클릭
9. 결과가 `Synced`로 표시되는지 확인

## 구현 원칙

- `PING`에 7일 영업시간을 넣지 않는다. `PING`은 RTC 보정 전용이다.
- 영업시간은 `SET_OPERATE_TIME` / `GET_OPERATE_TIME` 별도 명령으로 저장하고 읽는다.
- 서버 장애 시 마지막 정상 캐시를 사용한다.
- 첫 구현은 주간 영업시간만 처리한다.
- 공휴일, 시즌별 예외, 장치별 개별 스케줄은 이번 범위에 넣지 않는다.
- 서버의 `operate_times` 배열 순서를 믿지 말고 항상 `MON`~`SUN`으로 정렬해서 처리한다.
- PING timezone 설정은 관리자가 설정 화면에서 지정한 값을 사용한다. 서버 timezone은 자동으로 설정값을 바꾸지 않는다.
