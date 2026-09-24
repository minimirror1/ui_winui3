using System;
using System.IO;
using System.Linq;
using System.Text;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class LogFileReaderTests
{
    private static readonly DateOnly Day = new(2026, 9, 24);

    [TestMethod]
    public void Read_JoinsEveryFileOfThatDayInOrder()
    {
        using var temp = new TempFolder();
        WriteLog(temp.Path, Day, 0, "09:00:00.000,TX,3,Ping,,0,,");
        WriteLog(temp.Path, Day, 1, "10:00:00.000,TX,4,Ping,,0,,");

        var result = LogFileReader.Read(temp.Path, Day, LogFilter.None, maxRows: 100);

        CollectionAssert.AreEqual(
            new[] { "09:00:00.000", "10:00:00.000" },
            result.Rows.Select(r => r.Time).ToArray());
    }

    [TestMethod]
    public void Read_LeavesOutHeaderAndGapMarkers()
    {
        using var temp = new TempFolder();
        WriteLog(temp.Path, Day, 0, "# 대기줄이 차서 12줄 버림", "09:00:00.000,TX,3,Ping,,0,,");

        var result = LogFileReader.Read(temp.Path, Day, LogFilter.None, maxRows: 100);

        Assert.AreEqual(1, result.Rows.Count, "머리글과 구멍 표시는 줄로 세지 않는다");
    }

    [TestMethod]
    public void Read_FiltersByResult()
    {
        using var temp = new TempFolder();
        WriteLog(temp.Path, Day, 0,
            "09:00:00.000,RX,3,Pong,OK,10,43,",
            "09:00:01.000,RX,7,SaveFile,ERROR,1,1765,");

        var result = LogFileReader.Read(temp.Path, Day, new LogFilter(null, "ERROR"), maxRows: 100);

        Assert.AreEqual(1, result.Rows.Count);
        Assert.AreEqual("SaveFile", result.Rows[0].Command);
    }

    [TestMethod]
    public void Read_FiltersByDevice()
    {
        using var temp = new TempFolder();
        WriteLog(temp.Path, Day, 0,
            "09:00:00.000,RX,3,Pong,OK,10,43,",
            "09:00:01.000,RX,7,Pong,OK,10,51,");

        var result = LogFileReader.Read(temp.Path, Day, new LogFilter(7, null), maxRows: 100);

        Assert.AreEqual(1, result.Rows.Count);
        Assert.AreEqual("7", result.Rows[0].Device);
    }

    [TestMethod]
    public void Read_MoreMatchesThanAllowed_StopsAtTheCapAndSaysSo()
    {
        using var temp = new TempFolder();
        string[] many = Enumerable.Range(0, 500)
            .Select(i => $"09:00:{i % 60:D2}.000,TX,3,Ping,,0,,")
            .ToArray();
        WriteLog(temp.Path, Day, 0, many);

        var result = LogFileReader.Read(temp.Path, Day, LogFilter.None, maxRows: 100);

        Assert.AreEqual(100, result.Rows.Count, "상한을 넘겨 담지 않아야 한다");
        Assert.IsTrue(result.IsTruncated, "잘렸다는 사실을 알려야 한다");
    }

    [TestMethod]
    public void Read_QuotedValue_KeepsTheCommaInsideOneCell()
    {
        using var temp = new TempFolder();
        WriteLog(temp.Path, Day, 0, "09:00:00.000,RX,3,\"Save,File\",OK,1,5,");

        var result = LogFileReader.Read(temp.Path, Day, LogFilter.None, maxRows: 100);

        Assert.AreEqual("Save,File", result.Rows[0].Command);
    }

    [TestMethod]
    public void Read_DayWithNoRecord_ReturnsNothing()
    {
        using var temp = new TempFolder();

        var result = LogFileReader.Read(temp.Path, Day, LogFilter.None, maxRows: 100);

        Assert.AreEqual(0, result.Rows.Count);
        Assert.IsFalse(result.IsTruncated);
    }

    private static void WriteLog(string folder, DateOnly date, int sequence, params string[] rows)
    {
        string path = Path.Combine(folder, LogFilePolicy.FileName(date, sequence));
        var text = new StringBuilder();
        text.AppendLine(LogCsvLine.Header);
        foreach (string row in rows) text.AppendLine(row);
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aclog-read-" + Guid.NewGuid().ToString("N"));

        public TempFolder() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
