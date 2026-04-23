# WinUI3 Firmware Alignment Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Align the WinUI3 app with the newer STM32 firmware protocol so device status, file verification, file save acknowledgements, and error handling all match real firmware behavior.

**Architecture:** Treat `firmware_reference/stm32_json_com` as the protocol source of truth. First fix protocol enums/parsers in the WinUI app, then propagate those parsed values through `SerialService` and the device view models, and finally realign virtual mode so development and tests stop masking protocol mismatches.

**Tech Stack:** WinUI 3, CommunityToolkit.Mvvm, MSTest, custom binary protocol, XBee Fragment Protocol

## Progress Status

- [x] `VERIFY_FILE` response parsing aligned to firmware shape: `path_len(2) + path + match(1)`
- [x] `VerifyFileAsync` updated to validate the returned verify path before trusting the match flag
- [x] `VirtualDeviceManager.HandleVerifyFile` updated to emit firmware-shaped verify responses
- [x] Verify-focused tests added and passing
- [ ] PONG payload parsing and device status projection
- [ ] `SAVE_FILE` acknowledgement flow and returned-path validation
- [ ] Firmware error codes `0x06` / `0x07` and protocol size-limit enforcement

### Task 1: Align Protocol Types And Parsers

**Status:** In progress

**Completed in this task:**
- [x] `VERIFY_FILE` parsing now consumes `path_len(2) + path + match(1)` instead of treating the first payload byte as the match flag
- [ ] Firmware error codes `ResponseTooLarge = 0x06` and `TxBusy = 0x07`
- [ ] Parsed PONG status model and `ParsePongResponse`

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryProtocol.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryDeserializer.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BinaryProtocolCompatibilityTests.cs`
- Reference: `firmware_reference/stm32_json_com/Inc/binary_com.h`
- Reference: `firmware_reference/stm32_json_com/Inc/device_hal.h`
- Reference: `firmware_reference/stm32_json_com/Src/binary_com.c`

**Step 1: Write the failing tests**

Status: partially completed for the verify-response portion only.

Add MSTest cases that prove the app currently disagrees with firmware:

- `ParseVerifyFileResponse_reads_path_prefixed_payload_and_match_flag`
- `ParsePongResponse_reads_state_init_and_motion_times`
- `ParseErrorResponse_supports_response_too_large_and_tx_busy`

Use sample firmware-shaped payloads:

```csharp
byte[] verifyPayload = { 0x0A, 0x00, (byte)'S', (byte)'e', (byte)'t', (byte)'t', (byte)'i', (byte)'n', (byte)'g', (byte)'/', (byte)'A', 0x01 };
byte[] pongPayload = { 0x01, 0x03, 0x10, 0x27, 0x00, 0x00, 0x20, 0x4E, 0x00, 0x00 };
```

**Step 2: Run test to verify it fails**

Status: partially completed for verify-focused tests only.

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter BinaryProtocolCompatibilityTests
```

Expected: FAIL because `ParseVerifyFileResponse` reads `payload[0]` as the match flag and there is no PONG payload parser or new error codes.

**Step 3: Write minimal implementation**

Status: partially completed.

Implement the missing protocol contract:

- [ ] Add firmware error codes `ResponseTooLarge = 0x06` and `TxBusy = 0x07`
- [ ] Add a parsed PONG status model or record with:
  - `state`
  - `init_state`
  - `current_ms`
  - `total_ms`
- [ ] Add a `ParsePongResponse` method that reads the 10-byte firmware payload
- [x] Change verify parsing to consume `path_len(2) + path + match(1)` and return both path and match, not just a bare bool

Suggested signature:

```csharp
public static (string Path, bool Match) ParseVerifyFileResponse(ReadOnlySpan<byte> payload)
```

**Step 4: Run test to verify it passes**

Status: partially completed for verify-focused tests only.

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter BinaryProtocolCompatibilityTests
```

Expected: PASS

**Step 5: Commit**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryProtocol.cs AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryDeserializer.cs AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BinaryProtocolCompatibilityTests.cs
git commit -m "fix: align WinUI protocol parsers with firmware"
```

### Task 2: Propagate Firmware PONG Status Through The App

**Status:** Not started

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/SerialService.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Models/Device.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/ScanDialogViewModel.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/DeviceDetailViewModel.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/FirmwareStatusProjectionTests.cs`
- Reference: `firmware_reference/stm32_json_com/Inc/device_hal.h`
- Reference: `firmware_reference/stm32_json_com/Src/binary_com.c`

**Step 1: Write the failing tests**

Add unit tests around the status mapping logic:

- `Pong_status_playing_maps_to_device_motion_state_playing`
- `Pong_status_init_busy_maps_to_status_message`
- `Pong_times_map_from_milliseconds_to_timespan`

Keep the mapping in a small helper or method so it is testable without UI.

**Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter FirmwareStatusProjectionTests
```

Expected: FAIL because the current code only checks `hdr.Cmd == Pong` and hardcodes `"Online"` without reading payload.

**Step 3: Write minimal implementation**

Implement the actual status flow:

- Parse the PONG payload inside `SerialService.PingDeviceAsync`
- Populate `Device.StatusMessage`, `Device.MotionState`, `Device.MotionCurrentTime`, and `Device.MotionTotalTime` from firmware values
- Capture the XBee source address from the reply and store it in `Device.Address64`
- On device detail load, refresh device status once before or alongside file/motor loading
- Add a lightweight periodic status refresh path so the UI no longer relies on optimistic local state only

Recommended rule:

- `Play/Stop/Pause/Seek` commands may update the UI optimistically only if followed by a real status refresh
- The firmware PONG payload should be the long-term source of truth for motion status and time

**Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter FirmwareStatusProjectionTests
```

Expected: PASS

**Step 5: Commit**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/SerialService.cs AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Models/Device.cs AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/ScanDialogViewModel.cs AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/DeviceDetailViewModel.cs AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/FirmwareStatusProjectionTests.cs
git commit -m "feat: surface firmware device status in WinUI"
```

### Task 3: Make Save And Verify Use Real Firmware Responses

**Status:** In progress

**Completed in this task:**
- [x] Use the new verify parser result in `VerifyFileAsync`
- [x] Update `VirtualDeviceManager.HandleVerifyFile` to emit `path_len + path + match`
- [ ] Change `SaveFileAsync` to use `SendBinaryQueryAsync(..., BinaryCommand.SaveFile, packet)` and confirm the returned path
- [ ] Surface firmware failures in `FilesStatusMessage` and `LastLoadError`
- [ ] Update `VirtualDeviceManager.HandlePing` to emit a 10-byte PONG payload

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/DeviceDetailViewModel.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryDeserializer.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/VirtualDeviceManagerProtocolTests.cs`
- Reference: `firmware_reference/stm32_json_com/Src/binary_com.c`

**Step 1: Write the failing tests**

Status: partially completed for verify-response behavior only.

Add tests for the virtual device and file-response behavior:

- `Virtual_verify_response_matches_firmware_shape`
- `Virtual_ping_response_matches_firmware_shape`
- `Save_file_returns_path_confirmation_payload`

**Step 2: Run test to verify it fails**

Status: partially completed for verify-focused tests only.

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter VirtualDeviceManagerProtocolTests
```

Expected: FAIL because virtual PONG currently has an empty payload and virtual VERIFY_FILE returns a single-byte payload instead of `path + match`.

**Step 3: Write minimal implementation**

Status: partially completed.

Update the WinUI app behavior to match the firmware:

- [ ] Change `SaveFileAsync` to use `SendBinaryQueryAsync(..., BinaryCommand.SaveFile, packet)` and confirm the returned path
- [x] Use the new verify parser result in `VerifyFileAsync`
- [ ] Surface firmware failures in `FilesStatusMessage` and `LastLoadError`
- [ ] Update `VirtualDeviceManager.HandlePing` to emit a 10-byte PONG payload
- [x] Update `VirtualDeviceManager.HandleVerifyFile` to emit `path_len + path + match`

**Step 4: Run test to verify it passes**

Status: partially completed for verify-focused tests only.

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter VirtualDeviceManagerProtocolTests
```

Expected: PASS

**Step 5: Commit**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/DeviceDetailViewModel.cs AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/VirtualDeviceManager.cs AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryDeserializer.cs AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/VirtualDeviceManagerProtocolTests.cs
git commit -m "fix: use firmware-compatible save and verify responses"
```

### Task 4: Enforce Firmware Limits And Verify End-To-End Behavior

**Status:** Not started

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/DeviceDetailViewModel.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryProtocol.cs`
- Reference: `firmware_reference/stm32_json_com/Inc/binary_com.h`
- Reference: `firmware_reference/stm32_json_com/Inc/device_hal.h`

**Step 1: Write the failing test**

Add tests that assert the app recognizes firmware-side limits:

- content length must stay below `APP_CONTENT_MAX_LEN` (`512`)
- path length must stay below `APP_PATH_MAX_LEN` (`128`)
- error codes `ResponseTooLarge` and `TxBusy` become readable user-facing failures

Use a helper if necessary so the checks are testable without UI.

**Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter FirmwareStatusProjectionTests
```

Expected: FAIL because the app currently sends arbitrarily large content and does not know firmware codes `0x06` and `0x07`.

**Step 3: Write minimal implementation**

Implement guardrails:

- Add client-side validation before `SAVE_FILE` and `VERIFY_FILE`
- Reject oversized path/content before sending
- Show a clear UI message for:
  - `ResponseTooLarge`
  - `TxBusy`
  - `InvalidParam`
- Keep the limit constants near protocol code so they do not drift from firmware expectations

**Step 4: Run test to verify it passes**

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln
```

Expected: PASS

**Step 5: Commit**

```bash
git add AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/DeviceDetailViewModel.cs AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryProtocol.cs AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/FirmwareStatusProjectionTests.cs
git commit -m "fix: enforce firmware protocol limits in WinUI"
```

### Manual Verification

**Status:** Not started

After Task 4, verify against a real device:

1. Connect to the XBee module and scan a device ID that exists.
2. Confirm the dashboard/device page shows firmware-derived status instead of generic `"Online"`.
3. Trigger `Play`, `Pause`, `Stop`, and `Seek`; verify the UI follows the real PONG status after refresh.
4. Open a known `Setting/*.TXT` file, edit it, save it, and confirm the app reports the returned path.
5. Verify matching and mismatching file content and confirm the dialog reflects the actual firmware result.
6. Try an oversized edit and confirm the app blocks the send before transport.

Plan complete and saved to `docs/plans/2026-04-23-winui3-firmware-alignment.md`.
