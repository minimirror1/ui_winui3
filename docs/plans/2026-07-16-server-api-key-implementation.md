# Server API Key Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Store the backend API key in Windows PasswordVault and attach it as `X-API-Key` to every backend HTTP and SSE request.

**Architecture:** Extend the existing settings contract with an in-memory API-key property whose Windows persistence is delegated to a small credential-store abstraction. Keep the key out of `backend-settings.json`, and apply the header in the existing `BackendHttpRequest.Create` request factory so every backend client inherits the behavior. Add a clearly labelled masked field and secure-storage guidance to the existing connection settings UI.

**Tech Stack:** C# 12, .NET 8, WinUI 3, Windows PasswordVault, MSTest.

### Task 1: Define observable behavior with failing tests

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendServerCatalogClientTests.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsSourceTests.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter.Tests/BackendSettingsPageXamlTests.cs`

1. Add a request test that sets a fake API key and expects `X-API-Key` on the outgoing request.
2. Add a settings test proving the API key is not written to `backend-settings.json`.
3. Add a XAML contract test for a masked API-key field and secure-storage explanation.
4. Run only these tests and verify they fail because the API-key feature is absent.

### Task 2: Add PasswordVault-backed settings

**Files:**
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/IBackendApiKeyStore.cs`
- Create: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendApiKeyStore.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Core/Interfaces/ISettingsService.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/SettingsService.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/App.xaml.cs`

1. Introduce a synchronous key-store contract suitable for settings load/save.
2. Implement it with `Windows.Security.Credentials.PasswordVault` on Windows and a non-persistent fallback for unit tests.
3. Inject the store into `SettingsService`, load the key during `Load`, and save/remove it during `Save`.
4. Keep the API key out of the JSON settings record.

### Task 3: Apply the request header and settings UI

**Files:**
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/Infrastructure/BackendHttpRequest.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/ViewModels/BackendSettingsViewModel.cs`
- Modify: `AnimatronicsControlCenter/AnimatronicsControlCenter/UI/Views/BackendSettingsPage.xaml`

1. Trim a configured key and add it as `X-API-Key` in the common request factory.
2. Load and save the key through `BackendSettingsViewModel`.
3. Add a masked `PasswordBox` in a distinct API authentication group with concise PasswordVault guidance.
4. Run the focused tests and verify they pass.

### Task 4: Verify the complete change

**Files:**
- Verify all modified production and test files.

1. Run `dotnet test .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln` and require zero failures.
2. Run `dotnet build .\AnimatronicsControlCenter\AnimatronicsControlCenter.sln` and require zero errors.
3. Search tracked files to confirm the real API key was never written.
4. Review `git diff --check`, `git diff`, and `git status` for surgical scope.
