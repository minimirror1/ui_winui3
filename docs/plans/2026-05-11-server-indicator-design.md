# Server Indicator and Monitor Design

## Goal

Add a server indicator to the left sidebar and a dedicated server monitor page.
The indicator should show backend server status plus uplink and downlink activity.
The monitor page should show current server status and the latest backend traffic.

## Confirmed Behavior

- Create the work on `feature/server-indicator`.
- Add a sidebar server indicator near the existing serial connection, serial traffic indicator, and backend settings buttons.
- Match the existing serial traffic indicator style.
- Show three indicator dots:
  - Server status
  - Uplink activity
  - Downlink activity
- Blink uplink whenever the app sends backend traffic.
- Blink downlink whenever the app receives backend traffic.
- Treat any backend HTTP communication as traffic, regardless of endpoint or feature.
- Use a slightly visible blink window, around 300 ms.
- Clicking the server indicator navigates to a new server monitor page.
- Keep the latest 100 backend traffic records in memory.

## Server Status Rule

Server status is based on recent successful communication.
If a backend request succeeds within the configured recent-success window, the server is online.
If no successful backend communication is recent enough, the server is offline.

The first app state can be offline or unknown depending on implementation fit, but the visible rule after traffic starts should be recent-success based.

## Server Monitor Page

The new page should use a status-summary area above a traffic log list.

Status summary should include:

- Backend server URL
- Connection status
- Last success time
- Last failure time
- Latest error message

Traffic log list should include:

- Time
- Direction or phase
- HTTP method
- Path
- Status code
- Duration
- Result message

## Implementation Approach

Add a shared backend traffic state service, for example `BackendTrafficTap`.
Backend HTTP call sites record traffic through this service.
The sidebar indicator and server monitor page both read from the same service.

The service should expose:

- Uplink/downlink activity timestamps for blinking
- Last success/failure metadata
- A bounded in-memory log of the latest 100 records
- An event so UI components can refresh when traffic changes

The implementation should reuse the existing serial traffic indicator pattern where practical, but avoid refactoring serial code unless it is directly needed.

## Verification

Tests should cover:

- Uplink/downlink activity expires after the blink window.
- Recent successful backend communication marks the server online.
- Old success without recent communication marks the server offline.
- Traffic log is capped at 100 records.
- Main window XAML contains the new server indicator and navigates to the server monitor page.

Manual verification should cover:

- Backend settings requests blink the server indicator.
- Backend dashboard sync requests blink the server indicator.
- Clicking the indicator opens the server monitor page.
- The monitor page shows status and recent request records.
