using System;
using System.Collections.Specialized;
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

    [TestMethod]
    public void FormatTrafficEntries_ProducesTabSeparatedCopyText()
    {
        var entry = new ServerTrafficEntryViewModel(new AnimatronicsControlCenter.Core.Models.BackendTrafficEntry(
            DateTimeOffset.Parse("2026-05-11T10:00:00.123+09:00"),
            AnimatronicsControlCenter.Core.Models.BackendTrafficPhase.Response,
            HttpMethod.Get,
            "/v1/service/objects/obj-1/power",
            200,
            TimeSpan.FromMilliseconds(12),
            "SSE data: {\"power_status\":\"ON\"}"));

        string text = ServerMonitorViewModel.FormatTrafficEntries(new[] { entry });

        StringAssert.Contains(text, "Time\tPhase\tMethod\tPath\tStatus\tDuration\tMessage");
        StringAssert.Contains(text, "10:00:00.123\tResponse\tGET\t/v1/service/objects/obj-1/power\t200\t12 ms\tSSE data: {\"power_status\":\"ON\"}");
    }

    [TestMethod]
    public void Refresh_AppendsNewTrafficWithoutResettingList()
    {
        var trafficTap = new BackendTrafficTap();
        var settings = new SettingsService(new FakeBackendSettingsPathProvider(CreateTempSettingsPath()));
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");
        trafficTap.RecordRequest(HttpMethod.Get, new Uri("https://example.invalid/v1/service/stores"), now);
        var viewModel = new ServerMonitorViewModel(trafficTap, settings);
        viewModel.Refresh(now);

        int resetCount = 0;
        int addCount = 0;
        viewModel.TrafficEntries.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset) resetCount++;
            if (e.Action == NotifyCollectionChangedAction.Add) addCount++;
        };

        trafficTap.RecordResponse(HttpMethod.Get, new Uri("https://example.invalid/v1/service/stores"), 200, TimeSpan.FromMilliseconds(25), "OK", now.AddMilliseconds(10));
        viewModel.Refresh(now.AddMilliseconds(20));

        Assert.AreEqual(0, resetCount);
        Assert.AreEqual(1, addCount);
        Assert.AreEqual(2, viewModel.TrafficEntries.Count);
        Assert.AreEqual("Response", viewModel.TrafficEntries[1].Phase);
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
