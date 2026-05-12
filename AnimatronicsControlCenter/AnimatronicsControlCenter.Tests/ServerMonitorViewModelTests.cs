using System;
using System.IO;
using System.Net.Http;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Infrastructure;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class ServerMonitorViewModelTests
{
    [TestMethod]
    public void Refresh_ProjectsBackendStatusAndTrafficLogs()
    {
        var trafficTap = new BackendTrafficTap();
        var settings = new SettingsService(new FakeBackendSettingsPathProvider(CreateTempSettingsPath()));
        settings.BackendBaseUrl = "https://example.invalid";
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");
        trafficTap.RecordRequest(HttpMethod.Get, new Uri("https://example.invalid/v1/service/stores"), now);
        trafficTap.RecordResponse(HttpMethod.Get, new Uri("https://example.invalid/v1/service/stores"), 200, TimeSpan.FromMilliseconds(25), "OK", now.AddMilliseconds(10));
        var viewModel = new ServerMonitorViewModel(trafficTap, settings);

        viewModel.Refresh(now.AddSeconds(30));

        Assert.AreEqual("https://example.invalid", viewModel.ServerUrl);
        Assert.AreEqual("Online", viewModel.ConnectionStatus);
        Assert.AreEqual(2, viewModel.TrafficEntries.Count);
        Assert.AreEqual("GET", viewModel.TrafficEntries[1].Method);
        Assert.AreEqual("/v1/service/stores", viewModel.TrafficEntries[1].Path);
        Assert.AreEqual("200", viewModel.TrafficEntries[1].StatusCode);
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
        string directory = Path.Combine(Path.GetTempPath(), "ui_winui3_server_monitor_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "backend-settings.json");
    }
}
