using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.Infrastructure;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

/// 하루치 기록이 쌓인 상태에서도 화면이 멈춰 보이지 않아야 한다.
/// 상한까지만 담고 끊는 구조가 유지되는지 지킨다.
[TestClass]
public class LogViewerVolumeTests
{
    [TestMethod]
    public void Refresh_RealisticDayOfTraffic_DoesNotThrowAndStaysQuick()
    {
        using var temp = new TempSettings();

        // 장치 10대를 5초마다 확인하면 12시간에 17만 줄쯤 된다.
        WriteBigLog(temp.LogFolder, DateOnly.FromDateTime(DateTime.Now), rows: 170_000);

        var viewModel = new LogViewerViewModel(temp.Settings);

        var watch = Stopwatch.StartNew();
        viewModel.Refresh();
        watch.Stop();

        Console.WriteLine($"[repro] 읽은 줄 {viewModel.Rows.Count}, 잘림 {viewModel.IsTruncated}, {watch.ElapsedMilliseconds}ms");

        Assert.IsTrue(
            watch.ElapsedMilliseconds < 3000,
            $"화면이 멈춰 보일 만큼 오래 걸린다: {watch.ElapsedMilliseconds}ms");
    }

    private static void WriteBigLog(string folder, DateOnly date, int rows)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, LogFilePolicy.FileName(date, 0));

        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine(LogCsvLine.Header);
        for (int i = 0; i < rows; i++)
        {
            int device = (i % 10) + 1;
            bool isRequest = i % 2 == 0;
            writer.WriteLine(isRequest
                ? $"09:{(i / 60) % 60:D2}:{i % 60:D2}.000,TX,{device},Ping,,0,,"
                : $"09:{(i / 60) % 60:D2}:{i % 60:D2}.000,RX,{device},Pong,OK,10,43,");
        }
    }

    private sealed class TempSettings : IDisposable
    {
        private readonly string _root;

        public TempSettings()
        {
            _root = Path.Combine(Path.GetTempPath(), "aclog-repro-" + Guid.NewGuid().ToString("N"));
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
