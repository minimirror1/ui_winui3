# 운영 프로토콜 구현 계획

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**목표:** 영업시간 운영 데이터를 장치에 저장하고 다시 읽을 수 있도록 `SET_OPERATE_TIME` / `GET_OPERATE_TIME` 바이너리 프로토콜을 구현한다.

**구조:** `PING`은 RTC 시간 보정 전용으로 유지하고, 영업시간은 별도 바이너리 명령으로 전송한다. WinUI3는 서버/캐시에서 만든 내부 스케줄을 고정 길이 payload로 직렬화하고, 장치 응답의 checksum 또는 저장 스케줄을 역직렬화해 readback 검증에 사용한다.

**기술:** .NET 8, WinUI 3, MSTest, 기존 binary serial protocol, 기존 `SerialService`, `VirtualDeviceManager`.

## 범위

이 문서는 `docs/plans/2026-05-19-operating-hours-device-sync.md`에서 프로토콜 관련 내용만 분리한 계획이다.

포함:

- `SET_OPERATE_TIME` / `GET_OPERATE_TIME` 명령 ID
- 영업시간 payload 바이너리 구조
- serializer / deserializer
- `SerialService`의 장치 전송 및 읽기 API
- `VirtualDeviceManager`의 가상 장치 저장 및 readback
- timezone offset을 설정 화면의 `PingUtcOffsetMinutes`에서 가져오는 규칙

제외:

- 서버 DTO와 REST 조회
- 로컬 캐시 파일
- 수동 동기화 화면 UI/ViewModel
- 정각 자동 전송 스케줄러

## 확정 프로토콜

영업시간은 `PING` payload에 넣지 않는다. `PING`은 기존처럼 RTC 보정 전용으로 둔다.

명령:

```csharp
SetOperateTime = 0x30,
GetOperateTime = 0x31,
```

`SET_OPERATE_TIME` 요청 payload:

```text
format_version        uint8   // 1
timezone_offset_min   int16   // 예: 한국 540, little-endian
schedule_checksum     uint32  // little-endian
day_count             uint8   // 7
rows[7]
```

요일 row:

```text
day_of_week           uint8   // 1=MON ... 7=SUN
open_minutes          uint16  // 00:00부터 지난 분, little-endian
close_minutes         uint16  // 00:00부터 지난 분, little-endian
```

휴무는 별도 휴무 플래그 없이 `open_minutes == 0 && close_minutes == 0`으로 판정한다. 따라서 `00:00` ~ `00:00`은 24시간 영업이 아니라 휴무다.

`SET_OPERATE_TIME` 응답 payload:

```text
schedule_checksum     uint32  // 장치가 저장한 checksum, little-endian
```

`GET_OPERATE_TIME` 요청 payload:

```text
empty
```

`GET_OPERATE_TIME` 응답 payload:

```text
SET_OPERATE_TIME 요청 payload와 같은 구조
```

`timezone_offset_min`은 서버 timezone 문자열을 변환해서 만들지 않는다. 설정 화면에 저장된 `ISettingsService.PingUtcOffsetMinutes` 값을 그대로 사용한다.

## 작업 1: 프로토콜 상수와 명령 ID 추가

**파일:**

- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryProtocol.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursProtocolTests.cs`

**할 일:**

1. `BinaryCommand`에 `SetOperateTime = 0x30`, `GetOperateTime = 0x31`을 추가한다.
2. 영업시간 payload 크기 상수를 둔다.
   - header 영역: `format_version(1) + timezone_offset_min(2) + checksum(4) + day_count(1)` = 8 bytes
   - row 영역: `5 bytes * 7 days` = 35 bytes
   - 총 payload 크기: 43 bytes
3. 기존 `SaveFile` / `GetFile` 파일 저장 프로토콜과 섞지 않는다.

**테스트:**

- 명령 ID 값이 firmware 합의값과 같은지 확인
- payload 크기가 43 bytes인지 확인

실행:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter OperatingHoursProtocolTests
```

## 작업 2: SET_OPERATE_TIME 요청 serializer 구현

**파일:**

- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinarySerializer.cs`
- 참조: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/OperatingHours/OperatingHoursSchedule.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursProtocolTests.cs`

**할 일:**

1. `EncodeSetOperateTime(byte srcId, byte tarId, OperatingHoursSchedule schedule, int timezoneOffsetMinutes)`를 만든다.
2. `format_version`은 `1`로 쓴다.
3. `timezone_offset_min`은 `int16` little-endian으로 쓴다.
4. `schedule_checksum`은 `uint32` little-endian으로 쓴다.
5. `day_count`는 항상 `7`로 쓴다.
6. 요일은 `MON`~`SUN` 순서로 넣는다.
7. `day_of_week`는 `MON=1`부터 `SUN=7`까지로 인코딩한다.
8. 휴무일은 `open_minutes = 0`, `close_minutes = 0`으로 인코딩한다.
9. `open_minutes`, `close_minutes`는 `uint16` little-endian으로 쓴다.
10. 최종 packet command는 `BinaryCommand.SetOperateTime`이어야 한다.

**테스트:**

- payload 첫 byte가 `1`인지 확인
- timezone offset `540`이 little-endian으로 들어가는지 확인
- checksum이 little-endian으로 들어가는지 확인
- 7개 row가 `MON`~`SUN` 순서인지 확인
- 휴무 row의 `open_minutes`, `close_minutes`가 모두 `0`인지 확인
- packet header command가 `SetOperateTime`인지 확인

## 작업 3: GET_OPERATE_TIME 요청 serializer 구현

**파일:**

- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinarySerializer.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursProtocolTests.cs`

**할 일:**

1. `EncodeGetOperateTime(byte srcId, byte tarId)`를 만든다.
2. payload는 빈 배열로 둔다.
3. 최종 packet command는 `BinaryCommand.GetOperateTime`이어야 한다.

**테스트:**

- payload length가 `0`인지 확인
- packet header command가 `GetOperateTime`인지 확인

## 작업 4: 응답 parser 구현

**파일:**

- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryDeserializer.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursProtocolTests.cs`

**할 일:**

1. `ParseSetOperateTimeResponse(ReadOnlySpan<byte> payload)`를 만든다.
2. `SET_OPERATE_TIME` 응답 payload의 첫 4 bytes를 `uint32` checksum으로 읽는다.
3. `ParseOperatingHoursPayload(ReadOnlySpan<byte> payload)`를 만든다.
4. payload 길이가 43 bytes보다 짧으면 실패시킨다.
5. `format_version`이 `1`이 아니면 실패시킨다.
6. `day_count`가 `7`이 아니면 실패시킨다.
7. 7개 row를 읽어 `OperatingHoursDeviceSchedule` 형태로 반환한다.
8. `day_of_week` 값은 `1`~`7`만 허용한다.
9. `open_minutes == 0 && close_minutes == 0`이면 휴무로 판정한다.

**테스트:**

- `SET_OPERATE_TIME` 응답 checksum을 little-endian으로 읽는지 확인
- `GET_OPERATE_TIME` 응답 payload를 7일 스케줄로 읽는지 확인
- 잘못된 version, day count, payload length는 실패하는지 확인
- 인코딩 후 파싱하면 원본 스케줄과 같은 checksum/day/time 값이 나오는지 확인

## 작업 5: SerialService에 장치 전송/읽기 연결

**파일:**

- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/ISerialService.cs`
- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/SerialService.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursDeviceSyncServiceTests.cs`

**할 일:**

1. `ISerialService`에 영업시간 전송/읽기 메서드를 추가한다.

   ```csharp
   Task<OperatingHoursDeviceWriteResult> SetOperatingHoursAsync(int deviceId, OperatingHoursSchedule schedule);
   Task<OperatingHoursDeviceReadResult> GetOperatingHoursAsync(int deviceId);
   ```

2. `SetOperatingHoursAsync`는 `BinarySerializer.EncodeSetOperateTime`을 사용한다.
3. `timezoneOffsetMinutes` 인자는 `ISettingsService.PingUtcOffsetMinutes`를 사용한다.
4. 응답 command/status가 `SetOperateTime` + OK인지 확인한다.
5. 응답 checksum을 파싱해 요청 schedule checksum과 비교할 수 있게 반환한다.
6. `GetOperatingHoursAsync`는 `BinarySerializer.EncodeGetOperateTime`을 사용한다.
7. 응답 command/status가 `GetOperateTime` + OK인지 확인한다.
8. 응답 payload를 `ParseOperatingHoursPayload`로 파싱해 반환한다.
9. `SetOperateTime`, `GetOperateTime`은 장치 저장/읽기 작업이므로 짧은 timeout이 아니라 기존 장기 작업 timeout 그룹에 넣는다.

**테스트:**

- 전송 시 `PingUtcOffsetMinutes`가 payload에 들어가는지 확인
- ACK checksum이 schedule checksum과 같은 경우 성공으로 반환하는지 확인
- 응답 status가 Error면 실패로 반환하는지 확인
- 읽기 응답의 schedule checksum과 day rows가 반환되는지 확인

## 작업 6: VirtualDeviceManager 가상 장치 지원

**파일:**

- 수정: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/VirtualDeviceManager.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/VirtualDeviceManagerOperatingHoursTests.cs`

**할 일:**

1. `ProcessBinaryCommand`에서 `SetOperateTime`, `GetOperateTime`을 처리한다.
2. `SetOperateTime` 요청 payload를 `ParseOperatingHoursPayload`로 파싱한다.
3. 파싱한 스케줄을 장치 ID별로 저장한다.
4. `SetOperateTime` OK 응답 payload에는 저장한 `schedule_checksum`만 넣는다.
5. `GetOperateTime` 요청에는 저장된 스케줄을 `SET_OPERATE_TIME` 요청 payload와 같은 구조로 반환한다.
6. 저장된 스케줄이 없으면 Error 응답을 반환한다.

**테스트:**

- 가상 장치에 `SetOperateTime` 후 checksum ACK가 오는지 확인
- 같은 장치에 `GetOperateTime`을 보내면 저장된 payload가 돌아오는지 확인
- 장치별 저장값이 서로 섞이지 않는지 확인
- 저장값이 없는 장치에서 `GetOperateTime`을 호출하면 실패 응답인지 확인

## 작업 7: 전체 프로토콜 검증

**파일:**

- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursProtocolTests.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/VirtualDeviceManagerOperatingHoursTests.cs`
- 테스트: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/OperatingHoursDeviceSyncServiceTests.cs`

**검증:**

1. serializer 단위 테스트를 실행한다.
2. deserializer 단위 테스트를 실행한다.
3. `SerialService`가 가상 장치에 `SET_OPERATE_TIME`을 보내고 checksum ACK를 받는지 확인한다.
4. `SerialService`가 같은 장치에서 `GET_OPERATE_TIME`으로 readback하는지 확인한다.
5. readback checksum이 원본 schedule checksum과 같은지 확인한다.

실행:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter "OperatingHoursProtocolTests|VirtualDeviceManagerOperatingHoursTests|OperatingHoursDeviceSyncServiceTests"
```

전체 확인:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln
dotnet build .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln
```

## 구현 원칙

- `PING` 프로토콜은 수정하지 않는다.
- 서버 timezone 문자열로 `PingCountryCode`, `PingUtcOffsetMinutes`를 자동 변경하지 않는다.
- `timezone_offset_min`은 설정 화면의 `PingUtcOffsetMinutes`를 사용한다.
- 영업시간은 `SaveFile` / `GetFile` 파일 저장 방식으로 보내지 않는다.
- 장치에 저장된 값은 `GET_OPERATE_TIME` readback과 checksum으로 검증한다.
- 첫 구현은 주간 7일 영업시간만 처리한다.
- 공휴일, 시즌별 예외, 장치별 개별 스케줄은 이번 프로토콜 범위에 넣지 않는다.
