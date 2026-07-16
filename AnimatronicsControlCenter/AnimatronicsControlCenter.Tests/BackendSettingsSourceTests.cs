using System;
using System.Collections.Generic;
using System.IO;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendSettingsSourceTests
{
    [TestMethod]
    public void BackendSettings_LoadsDefaultsWhenFileIsMissing()
    {
        var settings = new SettingsService(new FakeBackendSettingsPathProvider(CreateTempSettingsPath()));

        settings.Load();

        Assert.IsTrue(settings.IsBackendSyncEnabled);
        Assert.AreEqual("https://robot-monitor-api.innergm.com", settings.BackendBaseUrl);
        Assert.AreEqual("pc_name_001", settings.BackendPcName);
        Assert.AreEqual("1.1.1.0", settings.BackendSoftwareVersion);
        Assert.AreEqual(0, settings.BackendDeviceObjectMappings.Count);
    }

    [TestMethod]
    public void BackendSettings_SaveAndLoad_RoundTripsValues()
    {
        string path = CreateTempSettingsPath();
        var first = new SettingsService(new FakeBackendSettingsPathProvider(path))
        {
            IsBackendSyncEnabled = false,
            BackendBaseUrl = "https://example.invalid",
            BackendStoreId = "store-1",
            BackendPcId = "pc-1",
            BackendPcName = "Main PC",
            BackendSoftwareVersion = "1.2.3.4",
            BackendDeviceObjectMappings = new Dictionary<int, string> { [2] = "obj-1" },
        };

        first.Save();

        var second = new SettingsService(new FakeBackendSettingsPathProvider(path));
        second.Load();

        Assert.IsFalse(second.IsBackendSyncEnabled);
        Assert.AreEqual("https://example.invalid", second.BackendBaseUrl);
        Assert.AreEqual("store-1", second.BackendStoreId);
        Assert.AreEqual("pc-1", second.BackendPcId);
        Assert.AreEqual("Main PC", second.BackendPcName);
        Assert.AreEqual("1.2.3.4", second.BackendSoftwareVersion);
        Assert.AreEqual("obj-1", second.BackendDeviceObjectMappings[2]);
    }

    [TestMethod]
    public void BackendSettings_SaveAndLoad_RoundTripsServerObjectCatalog()
    {
        string path = CreateTempSettingsPath();
        var first = new SettingsService(new FakeBackendSettingsPathProvider(path))
        {
            BackendServerObjects = new List<BackendServerObjectMappingSource>
            {
                new("obj-1", "Robot A"),
                new("obj-2", "Robot B"),
            },
        };

        first.Save();

        var second = new SettingsService(new FakeBackendSettingsPathProvider(path));
        second.Load();

        Assert.AreEqual(2, second.BackendServerObjects.Count);
        Assert.AreEqual("obj-1", second.BackendServerObjects[0].ObjectId);
        Assert.AreEqual("Robot A", second.BackendServerObjects[0].ObjectName);
        Assert.AreEqual("obj-2", second.BackendServerObjects[1].ObjectId);
        Assert.AreEqual("Robot B", second.BackendServerObjects[1].ObjectName);
    }

    [TestMethod]
    public void BackendSettings_Save_DoesNotWriteApiKeyToJson()
    {
        string path = CreateTempSettingsPath();
        var settings = new SettingsService(new FakeBackendSettingsPathProvider(path))
        {
            BackendApiKey = "api-key-must-stay-out-of-json"
        };

        settings.Save();

        string json = File.ReadAllText(path);
        Assert.IsFalse(json.Contains("api-key-must-stay-out-of-json", StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("backendApiKey", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void BackendSettings_SaveAndLoad_UsesCredentialStoreForApiKey()
    {
        string path = CreateTempSettingsPath();
        var keyStore = new FakeBackendApiKeyStore();
        var first = new SettingsService(new FakeBackendSettingsPathProvider(path), keyStore)
        {
            BackendApiKey = "api-key-1"
        };

        first.Save();
        var second = new SettingsService(new FakeBackendSettingsPathProvider(path), keyStore);
        second.Load();

        Assert.AreEqual("api-key-1", second.BackendApiKey);
    }

    [TestMethod]
    public void BackendApiKeyStore_MissingCredentialUsesExceptionFreeLookup()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "Infrastructure", "BackendApiKeyStore.cs"));

        StringAssert.Contains(code, "RetrieveAll()");
        Assert.IsFalse(code.Contains(".Retrieve(ResourceName, UserName)", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("FindAllByResource", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AppSettings_SaveAndLoad_UsesSeparateFileBesideBackendSettings()
    {
        string backendPath = CreateTempSettingsPath();
        var first = new SettingsService(new FakeBackendSettingsPathProvider(backendPath))
        {
            LastComPort = "COM9",
            LastBaudRate = 57600,
            Theme = "Dark",
            IsVirtualModeEnabled = true,
            IsLastPortAutoConnectEnabled = true,
            Language = "en-US",
            ResponseTimeoutSeconds = 3.5,
            IsPeriodicPingEnabled = false,
            PingIntervalSeconds = 12.5,
            PingCountryCode = "US",
            PingUtcOffsetMinutes = -300,
            ScanStartId = 3,
            ScanEndId = 12
        };

        first.Save();

        string appSettingsPath = Path.Combine(Path.GetDirectoryName(backendPath)!, "app-settings.json");
        Assert.AreEqual(appSettingsPath, first.AppSettingsFilePath);
        Assert.IsTrue(File.Exists(appSettingsPath));
        Assert.AreNotEqual(backendPath, appSettingsPath);

        var second = new SettingsService(new FakeBackendSettingsPathProvider(backendPath));
        second.Load();

        Assert.AreEqual("COM9", second.LastComPort);
        Assert.AreEqual(57600, second.LastBaudRate);
        Assert.AreEqual("Dark", second.Theme);
        Assert.IsTrue(second.IsVirtualModeEnabled);
        Assert.IsTrue(second.IsLastPortAutoConnectEnabled);
        Assert.AreEqual("en-US", second.Language);
        Assert.AreEqual(3.5, second.ResponseTimeoutSeconds);
        Assert.IsFalse(second.IsPeriodicPingEnabled);
        Assert.AreEqual(12.5, second.PingIntervalSeconds);
        Assert.AreEqual("US", second.PingCountryCode);
        Assert.AreEqual(-300, second.PingUtcOffsetMinutes);
        Assert.AreEqual(3, second.ScanStartId);
        Assert.AreEqual(12, second.ScanEndId);
    }

    [TestMethod]
    public void AppSettings_LoadsDefaultThemeWhenThemeIsMissing()
    {
        string backendPath = CreateTempSettingsPath();
        string appSettingsPath = Path.Combine(Path.GetDirectoryName(backendPath)!, "app-settings.json");
        File.WriteAllText(appSettingsPath, """
            {
              "lastComPort": "COM3",
              "lastBaudRate": 115200,
              "isVirtualModeEnabled": false,
              "isLastPortAutoConnectEnabled": false,
              "language": "ko-KR",
              "responseTimeoutSeconds": 2,
              "isPeriodicPingEnabled": true,
              "pingIntervalSeconds": 5,
              "pingCountryCode": "KR",
              "pingUtcOffsetMinutes": 540,
              "scanStartId": 1,
              "scanEndId": 10
            }
            """);

        var settings = new SettingsService(new FakeBackendSettingsPathProvider(backendPath));
        settings.Load();

        Assert.AreEqual("Default", settings.Theme);
    }

    [TestMethod]
    public void AppSettings_LoadsDefaultThemeWhenThemeIsUnknown()
    {
        string backendPath = CreateTempSettingsPath();
        string appSettingsPath = Path.Combine(Path.GetDirectoryName(backendPath)!, "app-settings.json");
        File.WriteAllText(appSettingsPath, """
            {
              "theme": "Blue",
              "lastComPort": "COM3",
              "lastBaudRate": 115200,
              "isVirtualModeEnabled": false,
              "isLastPortAutoConnectEnabled": false,
              "language": "ko-KR",
              "responseTimeoutSeconds": 2,
              "isPeriodicPingEnabled": true,
              "pingIntervalSeconds": 5,
              "pingCountryCode": "KR",
              "pingUtcOffsetMinutes": 540,
              "scanStartId": 1,
              "scanEndId": 10
            }
            """);

        var settings = new SettingsService(new FakeBackendSettingsPathProvider(backendPath));
        settings.Load();

        Assert.AreEqual("Default", settings.Theme);
    }

    [TestMethod]
    public void AppSettings_SaveAndLoad_RoundTripsLightTheme()
    {
        string backendPath = CreateTempSettingsPath();
        var first = new SettingsService(new FakeBackendSettingsPathProvider(backendPath))
        {
            Theme = "Light"
        };

        first.Save();

        var second = new SettingsService(new FakeBackendSettingsPathProvider(backendPath));
        second.Load();

        Assert.AreEqual("Light", second.Theme);
    }

    [TestMethod]
    public void AppSettings_SaveAndLoad_NormalizesScanRange()
    {
        string backendPath = CreateTempSettingsPath();
        var first = new SettingsService(new FakeBackendSettingsPathProvider(backendPath))
        {
            ScanStartId = 20,
            ScanEndId = 5
        };

        first.Save();

        var second = new SettingsService(new FakeBackendSettingsPathProvider(backendPath));
        second.Load();

        Assert.AreEqual(5, second.ScanStartId);
        Assert.AreEqual(20, second.ScanEndId);
    }

    [TestMethod]
    public void BackendSettings_InvalidJson_FallsBackToDefaults()
    {
        string path = CreateTempSettingsPath();
        File.WriteAllText(path, "{bad json");

        var settings = new SettingsService(new FakeBackendSettingsPathProvider(path));
        settings.Load();

        Assert.IsTrue(settings.IsBackendSyncEnabled);
        Assert.AreEqual("pc_name_001", settings.BackendPcName);
        Assert.AreEqual("1.1.1.0", settings.BackendSoftwareVersion);
    }

    private sealed class FakeBackendSettingsPathProvider : IBackendSettingsPathProvider
    {
        public FakeBackendSettingsPathProvider(string filePath)
        {
            BackendSettingsFilePath = filePath;
        }

        public string BackendSettingsFilePath { get; }
    }

    private sealed class FakeBackendApiKeyStore : IBackendApiKeyStore
    {
        private string _apiKey = string.Empty;

        public string Load() => _apiKey;

        public void Save(string apiKey) => _apiKey = apiKey;
    }

    private static string CreateTempSettingsPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ui_winui3_backend_settings_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "backend-settings.json");
    }

    private static string ProjectPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Could not find project file: {Path.Combine(segments)}");
        return string.Empty;
    }
}
