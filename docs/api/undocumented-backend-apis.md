# Undocumented Backend APIs

This document records backend API behavior observed from live traffic before it is available in an official API reference.

Rules for this file:
- Treat entries as observation-based until the backend contract is confirmed.
- Move confirmed contracts into the official API docs or implementation plans.
- Client code should parse these payloads defensively.

## Object Power SSE

### Purpose

The PC app appears to receive object power commands from the backend through an SSE subscription.

This endpoint is not for the PC app to send a power command to the backend. The app keeps an SSE connection open for each mapped object and receives commands pushed by the server.

### Request

```http
GET /v1/service/objects/{object_id}/power
Accept: text/event-stream
Authorization: Bearer {token}
```

The `Authorization` header is used when a backend bearer token is configured.

### Observed SSE Data

Observed from the server traffic log:

```json
{
  "eventId": "2026-05-12T23:45:57.169393074Z",
  "eventType": "COMMAND",
  "username": "STORE#0PJXDCZH92TF1#PC#0Q78PSAXDB77A#OBJECT#0Q78PXD1DB77B",
  "data": "{\"power_status\":\"ON\",\"store_id\":\"0PJXDCZH92TF1\",\"pc_id\":\"0Q78PSAXDB77A\",\"object_id\":\"0Q78PXD1DB77B\",\"object_name\":\"cow1\"}",
  "referenceType": "ON",
  "referenceId": "0QC0BJKP5VA11",
  "isBroadcast": false,
  "createdAt": "2026-05-12T23:45:57.169393074Z"
}
```

### Payload Notes

- The SSE `data:` value is an outer JSON object.
- The outer JSON `data` field is a JSON-encoded string and must be parsed again as inner JSON.
- `eventType` was observed as `COMMAND`.
- `referenceType` was observed as `ON` and appears to duplicate the inner `power_status`.
- `isBroadcast: false` appears to mean a command for a specific object.
- `createdAt` is a UTC timestamp. Example: `2026-05-12T23:45:57Z` is `2026-05-13 08:45:57` in KST.

Inner JSON example:

```json
{
  "power_status": "ON",
  "store_id": "0PJXDCZH92TF1",
  "pc_id": "0Q78PSAXDB77A",
  "object_id": "0Q78PXD1DB77B",
  "object_name": "cow1"
}
```

### Suggested Client Handling

1. Keep a `GET /v1/service/objects/{object_id}/power` SSE connection open.
2. Parse each SSE `data:` value as outer JSON.
3. Process only `eventType == "COMMAND"` until more event types are known.
4. Parse outer `data` as inner JSON.
5. Confirm inner `object_id` matches the subscribed object id.
6. Read inner `power_action` first. If it is absent, fall back to inner `power_status`.
7. Resolve the local device id from `BackendDeviceObjectMappings` by object id.
8. Convert the power command to `POWER_CTRL`.

Implemented client mapping:

| SSE value | Serial command |
|-----------|----------------|
| `ON` | `POWER_CTRL action=ON` |
| `OFF` | `POWER_CTRL action=OFF` |
| `REBOOT` | `POWER_CTRL action=REBOOT` |

`power_action` is preferred because `REBOOT` is a command, not a status. Existing payloads that use `power_status` for `ON` and `OFF` remain supported.

### Open Questions

- Confirm the observed shape for `power_status: "OFF"`.
- Confirm whether the backend will add `power_action: "REBOOT"` for reboot commands.
- Confirm whether `referenceType` can be trusted, or whether clients should use only inner `data.power_action` / `data.power_status`.
- Confirm handling rules for `isBroadcast: true`.
- Confirm SSE reconnect behavior, heartbeat/comment events, and event id replay support.
