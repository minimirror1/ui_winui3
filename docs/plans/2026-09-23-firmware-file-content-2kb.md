# 펌웨어 수정 지침 — 파일 내용 송수신 한계 512B → 2048B

대상: `stm32_json_com` (STM32 Binary Communication Library) 유지보수자
작성일: 2026-09-23
관련 앱 버전: AnimatronicsControlCenter SW v1.1.16.0 이후

---

## 요약

설정 파일을 잘림 없이 읽고 쓸 수 있도록 `APP_CONTENT_MAX_LEN` 을 512 → 2048 으로 올립니다.

- **wire format 변경 없음.** `content_len` 은 이미 uint16 이라 2047 을 그대로 실을 수 있습니다.
- **RAM 추가 비용 0.** 아래 계산 참조.
- 새 명령어, 새 필드, 버전 협상 **모두 불필요**합니다.

단, 버퍼를 키우는 것만으로는 **조용한 절단** 자체가 없어지지 않습니다. 2047 바이트를
넘는 파일에는 같은 일이 그대로 일어납니다. 그래서 2번 항목(절단을 에러로 알리기)을
같이 처리하기를 권합니다.

---

## 배경 — 지금 무슨 일이 일어나는가

2026-09-23 실측. `Setting/MT_ST.TXT` 를 읽은 응답 프레임입니다.

```
01 00 21 00 14 02 | 11 00 "Setting/MT_ST.TXT" | FF 01 | 41 43 32 2C ...
└ 헤더 6B ───────┘  └ path_len 17 ─────────┘  └ 511 ┘

payload_len = 0x0214 = 532   (실제 payload 532B — 일치)
content_len = 0x01FF = 511   (실제 content 511B — 일치)
status      = 0x00 (OK)
content 마지막 = "...,2016,2217,1\r\nDXL2,3.8,CW,4200,2048,3,1840,2"
```

마지막 줄이 필드 8개에서 끊겼습니다(다른 DXL2 줄은 11개). 프레임 자체는 완전하고
`content_len` 이 payload 앞쪽에 511 로 박혀 있으므로, **장치가 511 바이트만 보내기로
하고 그대로 보낸 것**입니다. 전송 계층 유실이나 PC 측 파싱 문제가 아닙니다.

원인은 `binary_com.c:839-847` 입니다.

```c
char *content_buf = g_binary_scratch.content;              /* char content[APP_CONTENT_MAX_LEN] = 512 */
bool ok = App_GetFile(path_buf, content_buf, APP_CONTENT_MAX_LEN);
...
BoundedCStrLen(content_buf, APP_CONTENT_MAX_LEN, &content_len);   /* → 511 */
```

`BoundedCStrLen` 은 NUL 을 못 찾은 경우(len == 512)에만 false 를 돌려줍니다. 511 + NUL
이면 정상 통과해서 `BIN_STATUS_OK` 가 나갑니다. **잘렸다는 신호가 응답 어디에도 없습니다.**

---

## 변경 사항

### 1. (필수) 버퍼 상한 올리기 — `Inc/device_hal.h`

```diff
-#define APP_CONTENT_MAX_LEN   512
+#define APP_CONTENT_MAX_LEN   2048
```

이 한 줄이 전부입니다. `binary_com.c` 의 `HandleGetFile` / `HandleSaveFile` /
`HandleVerifyFile` 은 모두 이 매크로를 참조하므로 경계 검사 코드는 손댈 필요가 없습니다.

#### RAM 영향: 없음

`g_binary_scratch` 는 **union** 입니다 (`binary_com.c:64-71`).

```c
typedef union {
    AppMotorInfo  motors[APP_MAX_MOTORS];   /* 32개 */
    AppMotorState states[APP_MAX_MOTORS];
    AppFileInfo   files[APP_MAX_FILES];     /* 64개 — union 크기를 결정 */
    char          content[APP_CONTENT_MAX_LEN];
} BinaryScratch_t;
```

`sizeof(AppFileInfo)` 계산 (ARM 기본 정렬):

| 필드 | 크기 | 오프셋 |
|---|---|---|
| `char name[64]` | 64 | 0 |
| `char path[128]` | 128 | 64 |
| `bool is_directory` | 1 | 192 |
| *(패딩)* | 3 | 193 |
| `uint32_t size` | 4 | 196 |
| `uint8_t depth` | 1 | 200 |
| *(패딩)* | 1 | 201 |
| `int16_t parent_index` | 2 | 202 |
| **합계** | **204** | |

`files[64]` = 204 × 64 = **13,056 바이트**. union 크기는 이미 약 13KB 이고
`content[512]` 는 그중 512 바이트만 쓰고 있습니다. **2048 로 올려도 union 안에
그대로 들어가므로 정적 RAM 사용량은 1 바이트도 늘지 않습니다.**

#### 프레임 한계 확인

`BIN_TX_BUFFER_SIZE` 와 `FRAG_MAX_MESSAGE_SIZE` 가 둘 다 4096 이므로 그게 상한입니다.
최악 경로 길이(`APP_PATH_MAX_LEN - 1` = 127) 기준:

| 프레임 | 계산 | 결과 | 4096 대비 |
|---|---|---|---|
| GET_FILE 응답 | `6 + 2 + 127 + 2 + 2047` | 2,184 B | 여유 1,912 B |
| SAVE_FILE 요청 | `5 + 2 + 127 + 2 + 2047` | 2,183 B | 여유 1,913 B |

넉넉합니다. 참고로 이 방식으로 올릴 수 있는 이론적 최대치는
`4096 - 6 - 2 - 127 - 2 = 3959`, 즉 `APP_CONTENT_MAX_LEN ≤ 3960` 입니다.
그 이상은 `BIN_TX_BUFFER_SIZE` 와 `FRAG_MAX_MESSAGE_SIZE` 를 함께 올려야 하고,
그때는 `FRAG_BITMAP_SIZE`(현재 64B = 512조각 = 15,360B)도 같이 확인해야 합니다.

---

### 2. (강력 권장) 절단을 에러로 알리기

버퍼를 2048 로 올려도 **2047 바이트를 넘는 파일은 여전히 조용히 잘립니다.** 지금은
PC 앱이 `CMD_GET_FILES` 의 `size` 와 비교해서 절단을 추정하고 저장을 막는 방식으로
막아두었지만, 장치가 직접 알려주는 게 맞습니다.

파일 크기를 얻는 수단은 **이미 있습니다.** `App_GetFiles` 가 `AppFileInfo.size` 를
채우고 있으므로, 거기서 쓰는 방법(FatFs 라면 `f_stat()` 의 `FILINFO.fsize`)을 그대로
재사용하면 됩니다. 레퍼런스 문서에 나오는 SD 헬퍼는 `SD_ReadFile` / `SD_WriteFile`
둘뿐이라 크기 조회 함수는 별도로 노출해야 합니다.

App 레이어에서 처리하는 쪽이 간단합니다. `device_real.c`:

```c
bool App_GetFile(const char *path, char *out_content, uint16_t max_len)
{
    FILINFO fno;
    if (f_stat(path, &fno) != FR_OK) {
        return false;                    /* 파일 없음 */
    }

    /* NUL 자리를 빼고도 담을 수 있어야 한다. 담지 못하면 자르지 말고 실패시킨다. */
    if (fno.fsize >= max_len) {
        return false;
    }

    if (SD_ReadFile(path, out_content, max_len) != 0) {
        return false;
    }
    out_content[fno.fsize] = '\0';
    return true;
}
```

다만 이러면 "파일 없음"과 "너무 큼"이 둘 다 `ERR_FILE_NOT_FOUND`(0x04) 로 나갑니다.
PC 앱 입장에서는 원인을 구분할 수 없으니, 구분이 필요하면 통신 레이어에서 처리하십시오.
`device_hal.h` 에 크기 조회 함수를 하나 추가하고 `binary_com.c` 의 `HandleGetFile`
앞쪽에서 거릅니다.

```c
/* device_hal.h — 신규. 실패 시 음수 */
int32_t App_GetFileSize(const char *path);
```

```c
/* binary_com.c HandleGetFile — App_GetFile 호출 직전 */
    int32_t actual_size = App_GetFileSize(path_buf);
    if (actual_size >= (int32_t)APP_CONTENT_MAX_LEN) {
        SendErrorResponse(ctx, src_id, (uint8_t)CMD_GET_FILE,
                          ERR_RESPONSE_TOO_LARGE, "File exceeds content buffer");
        return;
    }
```

`ERR_RESPONSE_TOO_LARGE`(0x06)는 이미 정의돼 있고 PC 앱도
"Device response was too large." 로 해석합니다. 새 에러 코드는 필요 없습니다.

**이 항목을 건너뛰어도 1번만으로 동작은 합니다.** 다만 2047 초과 파일에 대해
앱은 계속 크기 비교라는 간접 추정에 의존하게 됩니다.

---

### 3. (확인 필요) `App_GetFiles` 의 `size` 필드

PC 앱의 절단 감지가 **이 값에 의존합니다.** 다음을 확인해 주십시오.

- `AppFileInfo.size` 는 **SD 디렉터리 엔트리의 실제 파일 크기**여야 합니다.
- 읽어들인 바이트 수나 버퍼 크기를 넣으면 안 됩니다. 그러면 절단된 파일에 대해
  `size == 수신 바이트 수` 가 되어 앱의 감지가 **영구히 무력화**됩니다.
- 디렉터리는 0 입니다. 파일에 0 을 넣으면 앱이 "크기 미보고"로 보고 검사를 건너뜁니다.

현재 값이 무엇인지 확인하려면 `CMD_GET_FILES` 응답에서 각 엔트리의
`flags(1) parent_index(2 LE) size(4 LE) ...` 중 `size` 4바이트를 보면 됩니다.

---

### 4. (권장) `App_VerifyFile` 의 거짓 일치

레퍼런스 구현이 이렇습니다.

```c
char buffer[APP_CONTENT_MAX_LEN];
if (SD_ReadFile(path, buffer, sizeof(buffer)) != 0) return false;
*out_match = (strcmp(buffer, content) == 0);
```

파일이 버퍼보다 크면 잘린 앞부분끼리 비교하게 되어, **실제로는 다른 파일에
`match = 0x01`(일치)을 반환합니다.** 크기 비교를 먼저 하십시오.

```c
    FILINFO fno;
    if (f_stat(path, &fno) != FR_OK) {
        return false;
    }
    if (fno.fsize >= sizeof(buffer)) {
        *out_match = false;          /* 담을 수 없으면 일치일 수 없다 */
        return true;
    }
```

`strcmp` 비교 자체에도 함정이 하나 더 있습니다. `content` 는 NUL 종료 문자열이라
파일 중간에 0x00 바이트가 있으면 거기서 비교가 끝납니다. 설정 파일은 텍스트라
문제되지 않지만, 바이너리 파일로 확장할 계획이 있다면 길이 기반 `memcmp` 로
바꿔야 합니다.

---

### 5. (필수) `device_mock.c` 동기화

`AGENTS.md` 4번 절차에 따라 mock/weak 구현도 같이 갱신합니다. 특히 2번을 적용했다면
mock 의 `App_GetFile` 도 같은 실패 조건을 따라야 테스트가 실제 동작과 맞습니다.

---

## 변경하지 말아야 할 것

| 항목 | 현재 값 | 이유 |
|---|---|---|
| wire format | `path_len(2) path content_len(2) content` | `content_len` 이 uint16 이라 2047 을 이미 표현 가능. 바꾸면 기존 앱과 전부 깨집니다 |
| `BIN_TX_BUFFER_SIZE` | 4096 | 2184B 프레임에 여유 충분 |
| `FRAG_MAX_MESSAGE_SIZE` | 4096 | 위와 같음 |
| `FRAG_MAX_PAYLOAD` | 30 | DigiMesh 암호화 시 안전값. 건드리면 링크 신뢰성 문제 |
| `APP_PATH_MAX_LEN` | 128 | 프레임 한계 계산의 전제 |
| 명령어 ID | `0x21`/`0x22`/`0x23` | 변경 없음 |

---

## PC 앱 쪽 대응 (이미 반영됨)

| 항목 | 변경 |
|---|---|
| `BinaryProtocolConst.AppContentMaxLen` | 512 → 2048 |
| `MaxContentUtf8Bytes` | 511 → 2047 (자동) |
| `DeviceFrameMaxBytes` | 4096 신설 — 프레임 한계 초과를 테스트가 차단 |
| 절단 감지 + 저장 차단 | `FirmwareFileTruncationCheck` (2047 초과 파일용 안전망으로 유지) |

앱에는 `AppContentMaxLen` 을 3960 초과로 올리면 실패하는 테스트가 들어 있습니다
(`FirmwareContentLimit_LeavesRoomWithinDeviceFrameCeiling`).

---

## 호환성

| 조합 | 읽기 | 쓰기 |
|---|---|---|
| 구 펌웨어(512) + 구 앱(511) | 511B 에서 조용히 잘림 | 511B 초과 거부 |
| 구 펌웨어(512) + **신 앱(2047)** | 511B 에서 잘림 → **앱이 감지하고 저장 차단** | 512B 이상 보내면 펌웨어가 `ERR_INVALID_PARAM` 반환, 앱이 에러 표시. **데이터 손상 없음** |
| **신 펌웨어(2048)** + 구 앱(511) | 2047B 까지 정상 수신 (앱은 `content_len` 기준이라 문제 없음) | 앱이 511B 로 스스로 막음 — 안전하지만 기능 제한 |
| **신 펌웨어 + 신 앱** | 2047B 까지 정상 | 2047B 까지 정상 |

**어느 조합에서도 데이터가 손상되지 않습니다.** 펌웨어와 앱 업데이트 순서는 자유입니다.

---

## 검증 방법

1. **빌드 후 정적 확인**
   - `sizeof(BinaryScratch_t)` 가 변경 전후 동일한지 확인 (union 이므로 13,056 유지)
   - 맵 파일에서 `.bss` 증가가 없는지 확인

2. **경계값 읽기**
   - SD 카드에 정확히 2047 바이트 파일을 두고 `CMD_GET_FILE` → `content_len == 2047`, `status == OK`
   - 2048 바이트 파일 → (2번 적용 시) `ERR_RESPONSE_TOO_LARGE`, (미적용 시) 2047 로 잘림

3. **경계값 쓰기**
   - 2047 바이트 `CMD_SAVE_FILE` → `status == OK`, SD 에 2047 바이트 기록 확인
   - 2048 바이트 → `ERR_INVALID_PARAM`

4. **실파일 왕복**
   - `Setting/MT_ST.TXT` 를 앱에서 읽고 → 경고 InfoBar 가 뜨지 않는지 확인
   - `CMD_VERIFY_FILE` → `match == 0x01`
   - 한 줄 수정 후 저장 → 다시 읽어 반영 확인

5. **프레임 크기 실측**
   - 2047 바이트 응답의 총 프레임이 4096 미만인지 시리얼 모니터로 확인
   - 조각 수 = `ceil(2184 / 30)` ≈ 73개. `FRAG_SESSION_TIMEOUT_MS`(30초) 내 완료 확인
   - 실측 처리량 약 1.4KB/s 기준 약 1.6초 예상

6. **타임아웃 여유 확인 (앱 쪽은 확인 완료)**

   전송량이 4배로 늘어나므로 PC 앱의 응답 대기가 버티는지 봐야 합니다. 확인 결과
   `SerialService` 의 `ResponseTimeoutSeconds`(기본 2초)는 **총 소요 시간이 아니라
   비활성 타임아웃**입니다. `OnFragmentActivity` 가 뜰 때마다 `CancelAfter` 로 갱신되므로,
   조각이 2초 안에 계속 도착하기만 하면 전체 전송이 1.6초든 10초든 문제가 없습니다.
   실측 조각 간격은 약 21ms(538B / 385ms / 18조각)라 여유가 큽니다.

   **따라서 앱 설정 변경은 필요 없습니다.** 다만 펌웨어 쪽에서 SD 읽기를 한 번에
   2047바이트 하느라 조각 송출이 2초 이상 끊기면 타임아웃이 납니다. 파일을 읽는 동안
   fragment TX 가 멈추는 구조라면 읽기를 청크로 나누거나 읽기를 먼저 끝낸 뒤
   송출을 시작하도록 하십시오.

---

## `VERSION_HISTORY.md` 기재 템플릿

```markdown
## SW v1.1.x.0 - 2026-xx-xx

### 요약

파일 내용 송수신 한계를 512B → 2048B 로 확대. wire format 변경 없음.

### 프로토콜 변경

없음. `content_len` 은 기존과 동일한 uint16 이며 표현 가능 범위 내에서
실제 전송 길이만 늘어난다.

### App 레이어 계약

- `App_GetFile(path, out_content, max_len)`: `max_len` 이 2048 으로 커진다.
  out_content 는 반드시 NUL 종료해야 하며, 파일이 `max_len` 이상이면
  자르지 말고 false 를 반환한다.
- `App_GetFiles`: `AppFileInfo.size` 는 SD 디렉터리 기준 실제 파일 크기여야 한다.
  PC 앱의 절단 감지가 이 값에 의존한다.
- `App_VerifyFile`: 파일 크기가 버퍼 이상이면 비교하지 말고 out_match = false.

### 구현 위치

- `Inc/device_hal.h`: `APP_CONTENT_MAX_LEN` 512 → 2048
- `Src/device_real.c`: `App_GetFile` 크기 검사 추가
- `Src/device_mock.c`: 동일 조건 반영

### 수정할 때 지켜야 할 점

- `g_binary_scratch` 는 union 이고 `files[64]`(13,056B)가 크기를 결정하므로
  content 버퍼 확대에 추가 RAM 이 들지 않는다.
- GET_FILE 응답 최악값 `6 + 2 + 127 + 2 + 2047 = 2184B` 가
  `BIN_TX_BUFFER_SIZE`/`FRAG_MAX_MESSAGE_SIZE`(4096) 를 넘지 않아야 한다.
  이 방식의 상한은 `APP_CONTENT_MAX_LEN ≤ 3960`.

### 검증 방법

2047B/2048B 경계값 읽기·쓰기, 실파일 왕복, 프레임 크기 실측.

### 마이그레이션 노트

구/신 펌웨어와 구/신 앱의 4개 조합 모두에서 데이터 손상이 없다.
업데이트 순서 제약 없음.
```

---

## 참고

- 앱 측 절단 감지: `Core/Protocol/FirmwareFileTruncationCheck.cs`
- 앱 측 한계 상수: `Core/Protocol/BinaryProtocol.cs` (`BinaryProtocolConst`)
- 프로토콜 wire format: `docs/protocols/binary-protocol-spec.md` §4.7~4.9 (변경 없음)
