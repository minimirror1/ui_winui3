using System;
using System.Collections.Generic;
using System.IO;
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

    private static string CreateTempSettingsPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ui_winui3_backend_settings_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "backend-settings.json");
    }
}
