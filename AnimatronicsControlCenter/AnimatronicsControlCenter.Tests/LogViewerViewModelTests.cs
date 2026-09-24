using System;
using System.IO;
using System.Linq;
using System.Text;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.Infrastructure;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class LogViewerViewModelTests
{
    private static readonly DateOnly Day = new(2026, 9, 24);

    [TestMethod]
    public void Refresh_ShowsTheRowsOfTheChosenDay()
    {
        using var temp = new TempSettings();
        WriteLog(temp.LogFolder, Day,
            "09:00:00.000,RX,3,Pong,OK,10,43,",
            "09:00:01.000,RX,7,SaveFile,ERROR,1,1765,");

        var viewModel = new LogViewerViewModel(temp.Settings) { SelectedDate = Day };
        viewModel.Refresh();

        Assert.AreEqual(2, viewModel.Rows.Count);
    }

    [TestMethod]
    public void Refresh_OnlyFailures_LeavesOutTheRest()
    {
        using var temp = new TempSettings();
        WriteLog(temp.LogFolder, Day,
            "09:00:00.000,RX,3,Pong,OK,10,43,",
            "09:00:01.000,RX,7,SaveFile,ERROR,1,1765,");

        var viewModel = new LogViewerViewModel(temp.Settings)
        {
            SelectedDate = Day,
            SelectedResult = "ERROR",
        };
        viewModel.Refresh();

        Assert.AreEqual(1, viewModel.Rows.Count);
        Assert.AreEqual("SaveFile", viewModel.Rows[0].Command);
    }

    [TestMethod]
    public void Refresh_OneDevice_LeavesOutTheOthers()
    {
        using var temp = new TempSettings();
        WriteLog(temp.LogFolder, Day,
            "09:00:00.000,RX,3,Pong,OK,10,43,",
            "09:00:01.000,RX,7,Pong,OK,10,51,");

        var viewModel = new LogViewerViewModel(temp.Settings)
        {
            SelectedDate = Day,
            DeviceFilterText = "7",
        };
        viewModel.Refresh();

        Assert.AreEqual(1, viewModel.Rows.Count);
        Assert.AreEqual("7", viewModel.Rows[0].Device);
    }

    [TestMethod]
    public void Refresh_TooManyMatches_SaysItWasCutOff()
    {
        using var temp = new TempSettings();
        WriteLog(temp.LogFolder, Day, Enumerable.Range(0, LogViewerViewModel.MaxRows + 10)
            .Select(i => $"09:00:{i % 60:D2}.000,TX,3,Ping,,0,,")
            .ToArray());

        var viewModel = new LogViewerViewModel(temp.Settings) { SelectedDate = Day };
        viewModel.Refresh();

        Assert.AreEqual(LogViewerViewModel.MaxRows, viewModel.Rows.Count);
        Assert.IsTrue(viewModel.IsTruncated);
    }

    [TestMethod]
    public void Refresh_DayWithNothing_ClearsWhatWasShownBefore()
    {
        using var temp = new TempSettings();
        WriteLog(temp.LogFolder, Day, "09:00:00.000,RX,3,Pong,OK,10,43,");

        var viewModel = new LogViewerViewModel(temp.Settings) { SelectedDate = Day };
        viewModel.Refresh();
        viewModel.SelectedDate = Day.AddDays(1);
        viewModel.Refresh();

        Assert.AreEqual(0, viewModel.Rows.Count);
    }

    private static void WriteLog(string folder, DateOnly date, params string[] rows)
    {
        Directory.CreateDirectory(folder);
        var text = new StringBuilder();
        text.AppendLine(LogCsvLine.Header);
        foreach (string row in rows) text.AppendLine(row);
        File.WriteAllText(
            Path.Combine(folder, LogFilePolicy.FileName(date, 0)), text.ToString(), new UTF8Encoding(true));
    }

    private sealed class TempSettings : IDisposable
    {
        private readonly string _root;

        public TempSettings()
        {
            _root = Path.Combine(Path.GetTempPath(), "aclog-vm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            Settings = new SettingsService(new FakePathProvider(Path.Combine(_root, "backend-settings.json")));
            LogFolder = LogFilePolicy.FolderFor(Settings.AppSettingsFilePath);
        }

        public SettingsService Settings { get; }

        public string LogFolder { get; }

        public void Dispose()
        {
            try { Directory.Delete(_root, recursive: true); } catch { }
        }
    }

    private sealed class FakePathProvider : IBackendSettingsPathProvider
    {
        public FakePathProvider(string path) => BackendSettingsFilePath = path;

        public string BackendSettingsFilePath { get; }
    }
}
