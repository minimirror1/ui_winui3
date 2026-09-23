using System;
using System.IO;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class PingTimeZoneSettingsTests
{
    [TestMethod]
    public void PingTimeZoneId_DefaultsToSeoul()
    {
        var settings = new SettingsService(new FakeBackendSettingsPathProvider(CreateTempSettingsPath()));

        Assert.AreEqual("Asia/Seoul", settings.PingTimeZoneId);
    }

    [TestMethod]
    public void PingTimeZoneId_SaveAndLoad_RoundTrips()
    {
        string path = CreateTempSettingsPath();
        var first = new SettingsService(new FakeBackendSettingsPathProvider(path))
        {
            PingCountryCode = "US",
            PingTimeZoneId = "America/Denver",
        };

        first.Save();

        var second = new SettingsService(new FakeBackendSettingsPathProvider(path));
        second.Load();

        Assert.AreEqual("America/Denver", second.PingTimeZoneId);
    }

    [TestMethod]
    public void PingTimeZoneId_MigratesFromLegacyOffsetWhenMissing()
    {
        string backendPath = CreateTempSettingsPath();
        string appSettingsPath = Path.Combine(Path.GetDirectoryName(backendPath)!, "app-settings.json");
        File.WriteAllText(appSettingsPath, """
            {
              "lastComPort": "COM3",
              "lastBaudRate": 115200,
              "theme": "Default",
              "isVirtualModeEnabled": false,
              "isLastPortAutoConnectEnabled": false,
              "language": "ko-KR",
              "responseTimeoutSeconds": 2,
              "isPeriodicPingEnabled": true,
              "pingIntervalSeconds": 5,
              "pingCountryCode": "US",
              "pingUtcOffsetMinutes": -300,
              "scanStartId": 1,
              "scanEndId": 10
            }
            """);

        var settings = new SettingsService(new FakeBackendSettingsPathProvider(backendPath));
        settings.Load();

        Assert.AreEqual("America/New_York", settings.PingTimeZoneId,
            "Legacy US -300 settings must migrate to the Eastern timezone id.");
    }

    [TestMethod]
    public void PingTimeZoneId_UnknownLegacyOffset_FallsBackToSeoul()
    {
        string backendPath = CreateTempSettingsPath();
        string appSettingsPath = Path.Combine(Path.GetDirectoryName(backendPath)!, "app-settings.json");
        File.WriteAllText(appSettingsPath, """
            {
              "lastComPort": "COM3",
              "lastBaudRate": 115200,
              "theme": "Default",
              "isVirtualModeEnabled": false,
              "isLastPortAutoConnectEnabled": false,
              "language": "ko-KR",
              "responseTimeoutSeconds": 2,
              "isPeriodicPingEnabled": true,
              "pingIntervalSeconds": 5,
              "pingCountryCode": "ZZ",
              "pingUtcOffsetMinutes": 123,
              "scanStartId": 1,
              "scanEndId": 10
            }
            """);

        var settings = new SettingsService(new FakeBackendSettingsPathProvider(backendPath));
        settings.Load();

        Assert.AreEqual("Asia/Seoul", settings.PingTimeZoneId);
    }

    [TestMethod]
    public void PingTimeZoneId_StoredValueWinsOverLegacyOffset()
    {
        string backendPath = CreateTempSettingsPath();
        string appSettingsPath = Path.Combine(Path.GetDirectoryName(backendPath)!, "app-settings.json");
        File.WriteAllText(appSettingsPath, """
            {
              "lastComPort": "COM3",
              "lastBaudRate": 115200,
              "theme": "Default",
              "isVirtualModeEnabled": false,
              "isLastPortAutoConnectEnabled": false,
              "language": "ko-KR",
              "responseTimeoutSeconds": 2,
              "isPeriodicPingEnabled": true,
              "pingIntervalSeconds": 5,
              "pingCountryCode": "US",
              "pingUtcOffsetMinutes": -300,
              "pingTimeZoneId": "America/Chicago",
              "scanStartId": 1,
              "scanEndId": 10
            }
            """);

        var settings = new SettingsService(new FakeBackendSettingsPathProvider(backendPath));
        settings.Load();

        Assert.AreEqual("America/Chicago", settings.PingTimeZoneId);
    }

    private sealed class FakeBackendSettingsPathProvider : IBackendSettingsPathProvider
    {
        public FakeBackendSettingsPathProvider(string path) => BackendSettingsFilePath = path;

        public string BackendSettingsFilePath { get; }
    }

    private static string CreateTempSettingsPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "acc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "backend-settings.json");
    }
}
