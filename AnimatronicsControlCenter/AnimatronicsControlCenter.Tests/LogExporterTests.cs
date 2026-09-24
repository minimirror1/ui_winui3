using System;
using System.IO;
using System.Linq;
using System.Text;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class LogExporterTests
{
    private static readonly DateOnly Day = new(2026, 9, 24);

    [TestMethod]
    public void Export_JoinsEveryDayIntoOneFileWithASingleHeader()
    {
        using var temp = new TempFolder();
        WriteLog(temp.Path, Day.AddDays(-1), "08:00:00.000,TX,3,Ping,,0,,");
        WriteLog(temp.Path, Day, "09:00:00.000,TX,4,Ping,,0,,");
        string target = Path.Combine(temp.Path, "export.csv");

        LogExporter.Export(temp.Path, target);

        string[] lines = File.ReadAllLines(target).Where(l => l.Length > 0).ToArray();
        Assert.AreEqual(1, lines.Count(l => l == LogCsvLine.Header), "머리글은 한 번만 나와야 한다");
        Assert.AreEqual(3, lines.Length, "머리글 한 줄과 기록 두 줄");
    }

    [TestMethod]
    public void Export_PutsOlderDaysFirst()
    {
        using var temp = new TempFolder();
        WriteLog(temp.Path, Day, "09:00:00.000,TX,4,Ping,,0,,");
        WriteLog(temp.Path, Day.AddDays(-1), "08:00:00.000,TX,3,Ping,,0,,");
        string target = Path.Combine(temp.Path, "export.csv");

        LogExporter.Export(temp.Path, target);

        string[] lines = File.ReadAllLines(target).Where(l => l.Length > 0).ToArray();
        StringAssert.StartsWith(lines[1], "08:00:00.000");
        StringAssert.StartsWith(lines[2], "09:00:00.000");
    }

    [TestMethod]
    public void Export_KeepsGapMarkersSoHolesStayVisible()
    {
        using var temp = new TempFolder();
        WriteLog(temp.Path, Day, "# 대기줄이 차서 12줄 버림", "09:00:00.000,TX,3,Ping,,0,,");
        string target = Path.Combine(temp.Path, "export.csv");

        LogExporter.Export(temp.Path, target);

        StringAssert.Contains(File.ReadAllText(target), "12줄 버림");
    }

    [TestMethod]
    public void Export_NothingToExport_WritesJustTheHeader()
    {
        using var temp = new TempFolder();
        string target = Path.Combine(temp.Path, "export.csv");

        LogExporter.Export(temp.Path, target);

        string[] lines = File.ReadAllLines(target).Where(l => l.Length > 0).ToArray();
        CollectionAssert.AreEqual(new[] { LogCsvLine.Header }, lines);
    }

    private static void WriteLog(string folder, DateOnly date, params string[] rows)
    {
        var text = new StringBuilder();
        text.AppendLine(LogCsvLine.Header);
        foreach (string row in rows) text.AppendLine(row);
        File.WriteAllText(
            Path.Combine(folder, LogFilePolicy.FileName(date, 0)), text.ToString(), new UTF8Encoding(true));
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aclog-export-" + Guid.NewGuid().ToString("N"));

        public TempFolder() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
