# Server Indicator Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a sidebar server indicator and server monitor page that show backend status plus uplink/downlink activity.

**Architecture:** Add a shared backend traffic tap service that records request activity, response activity, recent success/failure, and the latest 100 traffic records. Existing backend HTTP clients record into the tap. MainWindow uses the tap for the compact indicator, and a new ServerMonitorPage displays status and logs.

**Tech Stack:** C#/.NET 8, WinUI 3 XAML, MSTest, Microsoft.Extensions.DependencyInjection.

### Task 1: Backend traffic state

**Files:**
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Models/BackendTrafficEntry.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Utilities/BackendTrafficState.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj`
- Test: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendTrafficStateTests.cs`

**Steps:**
1. Write failing tests for uplink/downlink activity expiration, recent-success online status, offline after stale success, and 100-record cap.
2. Run `dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln --filter BackendTrafficStateTests` and confirm compile/test failure because the types do not exist.
3. Add the model and state implementation.
4. Link the new source files in the test project.
5. Re-run the filtered tests and confirm they pass.

### Task 2: Backend traffic tap service

**Files:**
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IBackendTrafficTap.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendTrafficTap.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/App.xaml.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj`
- Test: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendTrafficTapTests.cs`

**Steps:**
1. Write failing tests that recording request/response raises `TrafficChanged` and exposes a snapshot/logs.
2. Run the filtered tests and confirm failure.
3. Implement the tap as a thin locked wrapper around `BackendTrafficState`.
4. Register it as singleton DI.
5. Re-run filtered tests.

### Task 3: HTTP call-site instrumentation

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendServerCatalogClient.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendMonitoringService.cs`
- Test: existing backend client/service tests

**Steps:**
1. Add failing tests to one catalog client test and one monitoring service test proving uplink and downlink are recorded.
2. Run the filtered tests and confirm failure.
3. Inject `IBackendTrafficTap` into backend HTTP clients and record request/response/error events.
4. Re-run filtered tests.

### Task 4: Sidebar server indicator

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/MainWindow.xaml`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/MainWindow.xaml.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/MainWindowLayoutTests.cs`

**Steps:**
1. Add failing XAML layout tests for `ServerTrafficButton` and three dots.
2. Run the layout tests and confirm failure.
3. Add the XAML control, timer, tap subscription, tooltip, and navigation handler.
4. Re-run layout tests.

### Task 5: Server monitor page

**Files:**
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/ServerMonitorViewModel.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/ServerMonitorPage.xaml`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/ServerMonitorPage.xaml.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/App.xaml.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/MainWindow.xaml.cs`
- Test: add focused view model and XAML tests

**Steps:**
1. Add failing tests for the view model summary/log projection and XAML text/list presence.
2. Run filtered tests and confirm failure.
3. Implement the view model and page.
4. Wire indicator click to `ServerMonitorPage`.
5. Re-run filtered tests.

### Task 6: Full verification

**Files:**
- All touched files.

**Steps:**
1. Run `dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln`.
2. Run `dotnet build .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln`.
3. Fix failures with TDD when they expose behavior gaps.
