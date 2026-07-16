# API Key Startup Prompt Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Prompt for a missing backend API key on every app launch while allowing the user to defer and continue using non-server features.

**Architecture:** Add a focused startup `ContentDialog` with a masked field, inline guidance, save validation, and a `나중에` close action. Activate the main window before showing the dialog, persist a submitted key through the existing settings service, and only start background backend services when a key exists. Keep service-level guards so background HTTP/SSE loops cannot start without authentication.

**Tech Stack:** C# 12, .NET 8, WinUI 3 ContentDialog, CommunityToolkit.Mvvm, MSTest.

### Task 1: Define prompt and startup contracts with failing tests

**Files:**
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendApiKeyPromptViewModelTests.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendApiKeyStartupPromptTests.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/AnimatronicsControlCenter.Tests.csproj`

1. Test that whitespace input cannot be saved and trimmed input can be saved.
2. Test the dialog XAML contract: masked input, reveal option, `저장`, and `나중에`.
3. Test the app startup source contract: activate the window, prompt only for a missing key, save a submitted key, and start backend services afterward.
4. Test that each background backend service refuses to start when the key is missing.
5. Run focused tests and verify RED failures caused by the missing feature.

### Task 2: Implement the prompt UI and startup flow

**Files:**
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendApiKeyPromptViewModel.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendApiKeyPromptDialog.xaml`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendApiKeyPromptDialog.xaml.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/App.xaml.cs`

1. Implement trimmed input and `CanSave` state in the view model.
2. Build an accessible ContentDialog with concise security and deferral guidance.
3. Make `OnLaunched` asynchronous, activate the window, prompt when the key is empty, and save only on the primary result.
4. Start backend background services only when the key exists after the prompt.

### Task 3: Guard background backend services

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendPowerSseService.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendDashboardSyncService.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/OperatingHoursAutoSyncService.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/MainWindow.xaml.cs`

1. Return from each service `Start` method when the API key is blank.
2. Remove the unconditional operating-hours start from the main-window constructor.
3. Run focused tests and verify GREEN.

### Task 4: Verify and commit

1. Run the complete solution test suite.
2. Build the complete WinUI solution with zero warnings and errors.
3. Verify the real key is absent from source, configuration, documentation, and Git diff.
4. Run `git diff --check`, review the diff, and commit the startup prompt change.
