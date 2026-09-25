using System;
using System.IO;
using System.Text;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

/// <summary>
/// 기록은 앱이 도는 내내 열려 있다. 그 상태로 같은 파일을 열어 볼 수 있어야 한다.
///
/// 2026-09-25: 기록 보기가 공유를 허용하지 않고 파일을 열어 IOException 이 났고,
/// 페이지 생성자에서 터지는 바람에 매장에서 앱이 그대로 종료됐다.
/// </summary>
[TestClass]
public class LogReadWhileWritingTests
{
    private static readonly DateOnly Day = new(2026, 9, 25);

    [TestMethod]
    public void Read_WhileTheLoggerHoldsTheFileOpen_StillWorks()
    {
        using var temp = new TempFolder();

        using (var logger = new TrafficFileLogger(
                   temp.Path,
                   new TrafficFileLoggerOptions(long.MaxValue, 1000, IncludeRaw: false)))
        {
            logger.Record(new Core.Models.SerialTrafficEntry(
                new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.FromHours(9)),
                Core.Models.SerialTrafficDirection.Tx,
                "00 03 01 00 00"));

            WaitForFile(Path.Combine(temp.Path, LogFilePolicy.FileName(Day, 0)));

            // 기록기가 파일을 연 채로 있는 지금 읽어야 한다.
            LogReadResult result = LogFileReader.Read(temp.Path, Day, LogFilter.None, 100);

            Assert.AreEqual(1, result.Rows.Count, "기록 중에도 읽을 수 있어야 한다");
        }
    }

    [TestMethod]
    public void Export_WhileTheLoggerHoldsTheFileOpen_StillWorks()
    {
        using var temp = new TempFolder();
        string target = Path.Combine(temp.Path, "export.csv");

        using (var logger = new TrafficFileLogger(
                   temp.Path,
                   new TrafficFileLoggerOptions(long.MaxValue, 1000, IncludeRaw: false)))
        {
            logger.Record(new Core.Models.SerialTrafficEntry(
                new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.FromHours(9)),
                Core.Models.SerialTrafficDirection.Tx,
                "00 03 01 00 00"));

            WaitForFile(Path.Combine(temp.Path, LogFilePolicy.FileName(Day, 0)));

            LogExporter.Export(temp.Path, target);
        }

        StringAssert.Contains(File.ReadAllText(target), LogCsvLine.Header);
    }

    private static void WaitForFile(string path)
    {
        for (int i = 0; i < 100 && !File.Exists(path); i++) System.Threading.Thread.Sleep(20);
        System.Threading.Thread.Sleep(50);
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aclog-share-" + Guid.NewGuid().ToString("N"));

        public TempFolder() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
