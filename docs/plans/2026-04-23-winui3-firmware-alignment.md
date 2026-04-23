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
- [x] PONG payload parsing and device status projection
- [x] Virtual `PONG` responses aligned to firmware shape
- [x] `SAVE_FILE` acknowledgement flow and returned-path validation
- [x] Firmware error codes `0x06` / `0x07`
- [x] Protocol size-limit enforcement
- [x] Lightweight periodic status refresh path

### Task 1: Align Protocol Types And Parsers

**Status:** Completed

**Completed in this task:**
- [x] `VERIFY_FILE` parsing now consumes `path_len(2) + path + match(1)` instead of treating the first payload byte as the match flag
- [x] Firmware error codes `ResponseTooLarge = 0x06` and `TxBusy = 0x07`
- [x] Parsed PONG status model and `ParsePongResponse`

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryProtocol.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryDeserializer.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BinaryProtocolCompatibilityTests.cs`
- Reference: `firmware_reference/stm32_json_com/Inc/binary_com.h`
- Reference: `firmware_reference/stm32_json_com/Inc/device_hal.h`
- Reference: `firmware_reference/stm32_json_com/Src/binary_com.c`

**Step 1: Write the failing tests**

Status: completed.

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

Status: completed.

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter BinaryProtocolCompatibilityTests
```

Expected: FAIL because `ParseVerifyFileResponse` reads `payload[0]` as the match flag and there is no PONG payload parser or new error codes.

**Step 3: Write minimal implementation**

Status: completed.

Implement the missing protocol contract:

- [x] Add firmware error codes `ResponseTooLarge = 0x06` and `TxBusy = 0x07`
- [x] Add a parsed PONG status model or record with:
  - `state`
  - `init_state`
  - `current_ms`
  - `total_ms`
- [x] Add a `ParsePongResponse` method that reads the 10-byte firmware payload
- [x] Change verify parsing to consume `path_len(2) + path + match(1)` and return both path and match, not just a bare bool

Suggested signature:

```csharp
public static (string Path, bool Match) ParseVerifyFileResponse(ReadOnlySpan<byte> payload)
```

**Step 4: Run test to verify it passes**

Status: completed.

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

**Status:** Completed

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

- [x] Parse the PONG payload inside `SerialService.PingDeviceAsync`
- [x] Populate `Device.StatusMessage`, `Device.MotionState`, `Device.MotionCurrentTime`, and `Device.MotionTotalTime` from firmware values
- [x] Capture the XBee source address from the reply and store it in `Device.Address64`
- [x] On device detail load, refresh device status once before file/motor loading
- [x] Add a lightweight periodic status refresh path so the UI no longer relies on optimistic local state only

Recommended rule:

- [x] `Play/Stop/Pause/Seek` commands may update the UI optimistically only if followed by a real status refresh
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

**Status:** Completed

**Completed in this task:**
- [x] Use the new verify parser result in `VerifyFileAsync`
- [x] Update `VirtualDeviceManager.HandleVerifyFile` to emit `path_len + path + match`
- [x] Change `SaveFileAsync` to use `SendBinaryQueryAsync(..., BinaryCommand.SaveFile, packet)` and confirm the returned path
- [x] Surface firmware failures in `FilesStatusMessage` and `LastLoadError`
- [x] Update `VirtualDeviceManager.HandlePing` to emit a 10-byte PONG payload

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/DeviceDetailViewModel.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryDeserializer.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/VirtualDeviceManagerProtocolTests.cs`
- Reference: `firmware_reference/stm32_json_com/Src/binary_com.c`

**Step 1: Write the failing tests**

Status: completed.

Add tests for the virtual device and file-response behavior:

- `Virtual_verify_response_matches_firmware_shape`
- `Virtual_ping_response_matches_firmware_shape`
- `Save_file_returns_path_confirmation_payload`

**Step 2: Run test to verify it fails**

Status: completed.

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter VirtualDeviceManagerProtocolTests
```

Expected: FAIL because virtual PONG currently has an empty payload and virtual VERIFY_FILE returns a single-byte payload instead of `path + match`.

**Step 3: Write minimal implementation**

Status: completed.

Update the WinUI app behavior to match the firmware:

- [x] Change `SaveFileAsync` to use `SendBinaryQueryAsync(..., BinaryCommand.SaveFile, packet)` and confirm the returned path
- [x] Use the new verify parser result in `VerifyFileAsync`
- [x] Surface firmware failures in `FilesStatusMessage` and `LastLoadError`
- [x] Update `VirtualDeviceManager.HandlePing` to emit a 10-byte PONG payload
- [x] Update `VirtualDeviceManager.HandleVerifyFile` to emit `path_len + path + match`

**Step 4: Run test to verify it passes**

Status: completed.

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

**Status:** Completed

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/DeviceDetailViewModel.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Protocol/BinaryProtocol.cs`
- Reference: `firmware_reference/stm32_json_com/Inc/binary_com.h`
- Reference: `firmware_reference/stm32_json_com/Inc/device_hal.h`

**Step 1: Write the failing test**

Status: completed.

Add tests that assert the app recognizes firmware-side limits:

- content length must stay below `APP_CONTENT_MAX_LEN` (`512`)
- path length must stay below `APP_PATH_MAX_LEN` (`128`)
- error codes `ResponseTooLarge` and `TxBusy` become readable user-facing failures

Use a helper if necessary so the checks are testable without UI.

**Step 2: Run test to verify it fails**

Status: completed.

Run:

```powershell
dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter FirmwareStatusProjectionTests
```

Expected: FAIL because the app currently sends arbitrarily large content and does not know firmware codes `0x06` and `0x07`.

**Step 3: Write minimal implementation**

Status: completed.

Implement guardrails:

- [x] Add client-side validation before `SAVE_FILE` and `VERIFY_FILE`
- [x] Reject oversized path/content before sending
- [x] Show a clear UI message for:
  - `ResponseTooLarge`
  - `TxBusy`
  - `InvalidParam`
- [x] Keep the limit constants near protocol code so they do not drift from firmware expectations

**Step 4: Run test to verify it passes**

Status: completed.

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

**Status:** Ready for hardware verification

Use this section as the real-device runbook. The code work is complete; only on-device confirmation remains.

**Pre-check**

- [ ] XBee module is connected and the WinUI app can open the correct COM port
- [ ] At least one real device ID is known and responds to `SCAN`
- [ ] At least one editable file under `Setting/*.TXT` exists on the device
- [ ] Use a small text edit first so protocol-limit failures do not mask basic save/verify behavior

**Runbook**

1. Connect and scan a real device
   - Action:
     - Launch the WinUI app
     - Connect to the XBee transport
     - Scan for the known device ID and open its detail page
   - Expected:
     - Device detail load finishes without protocol errors
     - Device status is firmware-derived, not the old generic `"Online"`
     - Expected status text is one of: `Playing`, `Stopped`, `Initializing`, `Ready`, `Error`
   - Failure clues:
     - Status stays blank or generic
     - File/motor snapshot never loads
     - Repeated transport error in the detail page

2. Confirm periodic status refresh is alive
   - Action:
     - Keep the detail page open for at least 3 seconds without changing selection
     - If the device is already playing, watch the motion time fields
   - Expected:
     - Status refresh continues while the page remains open
     - During playback, current motion time advances without needing manual refresh
     - The page no longer relies only on optimistic local state after a prior button press
   - Failure clues:
     - Motion time freezes while firmware is known to be playing
     - Status changes on the device but not in the app until page reload

3. Verify motion command round-trip
   - Action:
     - Trigger `Play`
     - Trigger `Pause`
     - Trigger `Stop`
     - Trigger `Seek` to a visible offset
   - Expected:
     - After each command, the app refreshes from firmware state
     - `Play` leads to `Playing`
     - `Stop` leads to `Stopped`
     - `Pause` and initialization-related states do not regress to the old fake `"Online"` state
     - After `Seek`, the reported current time reflects the new position or the firmware's actual accepted position
   - Failure clues:
     - Button press changes UI immediately but the next refresh snaps to a contradictory state
     - `Seek` does not affect reported current time
     - Status text does not match the firmware behavior

4. Verify file load and save acknowledgement path
   - Action:
     - Open a known `Setting/*.TXT` file
     - Make a minimal edit
     - Save the file
   - Expected:
     - File load succeeds and the editor shows the device content
     - Save succeeds with a path-confirming message in the format `Saved file: <path>`
     - No silent success on malformed or mismatched save response
   - Failure clues:
     - Save appears to work with no returned-path confirmation
     - Save fails with `invalid device response` even though firmware saved the file
     - Returned path does not match the selected file path

5. Verify matching content response
   - Action:
     - Without changing the just-saved content, run `Verify`
   - Expected:
     - Verify dialog shows `Content Matches Device`
     - No path mismatch or malformed-payload error appears
   - Failure clues:
     - Match result is inconsistent for identical content
     - Dialog shows `Verification failed: invalid device response.`

6. Verify mismatching content response
   - Action:
     - Edit the local text again without saving
     - Run `Verify`
   - Expected:
     - Verify dialog shows `Content Mismatch`
     - The app does not report a false match
   - Failure clues:
     - Device mismatch is reported as a match
     - Verify failure path is taken when payload is otherwise valid

7. Verify protocol limit enforcement
   - Action:
     - Increase content length beyond the firmware limit (`APP_CONTENT_MAX_LEN = 512`, effective payload content budget `511` bytes)
     - Attempt `Save`
     - Attempt `Verify`
   - Expected:
     - The app blocks the request before transport
     - Save shows `Failed to save file: ...`
     - Verify shows `Verification failed: ...`
     - A device round-trip is not required for this rejection
   - Failure clues:
     - Oversized content is transmitted to firmware
     - The app waits for a device reply before rejecting obviously oversized content

8. Record outcome
   - [ ] Status refresh on real device confirmed
   - [ ] Motion commands confirmed against real firmware state
   - [ ] Save returned-path acknowledgement confirmed
   - [ ] Verify match/mismatch confirmed
   - [ ] Client-side size-limit enforcement confirmed
   - [ ] Any mismatch captured with device ID, file path, exact UI message, and reproduction steps

**If a failure is found**

- Record the device ID
- Record the exact file path involved
- Record the exact UI message shown by the app
- Note whether the issue reproduces in virtual mode or only on the real device
- Note whether the failure happens on `Play`, `Stop`, `Pause`, `Seek`, `Save`, or `Verify`

Plan complete and saved to `docs/plans/2026-04-23-winui3-firmware-alignment.md`.
