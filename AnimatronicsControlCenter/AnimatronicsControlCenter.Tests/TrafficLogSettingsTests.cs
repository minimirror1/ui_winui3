using System;
using System.IO;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class TrafficLogSettingsTests
{
    [TestMethod]
    public void TrafficLog_IsOnByDefault()
    {
        var settings = new SettingsService(new FakePathProvider(TempSettingsPath()));

        settings.Load();

        Assert.IsTrue(settings.IsTrafficLogEnabled, "기록은 기본으로 켜져 있어야 한다");
    }

    [TestMethod]
    public void TrafficLogRaw_IsOffByDefault()
    {
        var settings = new SettingsService(new FakePathProvider(TempSettingsPath()));

        settings.Load();

        Assert.IsFalse(settings.IsTrafficLogRawIncluded, "원문 칸은 기본으로 비어 있어야 한다");
    }

    [TestMethod]
    public void TrafficLogSettings_SurviveSaveAndLoad()
    {
        string path = TempSettingsPath();
        var first = new SettingsService(new FakePathProvider(path))
        {
            IsTrafficLogEnabled = false,
            IsTrafficLogRawIncluded = true,
        };

        first.Save();

        var second = new SettingsService(new FakePathProvider(path));
        second.Load();

        Assert.IsFalse(second.IsTrafficLogEnabled);
        Assert.IsTrue(second.IsTrafficLogRawIncluded);
    }

    private static string TempSettingsPath()
        => Path.Combine(Path.GetTempPath(), "aclog-settings-" + Guid.NewGuid().ToString("N"), "backend-settings.json");

    private sealed class FakePathProvider : IBackendSettingsPathProvider
    {
        public FakePathProvider(string path) => BackendSettingsFilePath = path;

        public string BackendSettingsFilePath { get; }
    }
}
