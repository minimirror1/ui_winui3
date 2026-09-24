using System;
using System.IO;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class LogFileCleanerTests
{
    private static readonly DateOnly Today = new(2026, 9, 24);

    [TestMethod]
    public void Clean_RemovesFilesPastTheRetentionPeriod()
    {
        using var temp = new TempFolder();
        string old = Write(temp.Path, Today.AddDays(-101), 10);
        string recent = Write(temp.Path, Today.AddDays(-1), 10);

        LogFileCleaner.Clean(temp.Path, Today, retentionDays: 100, maxTotalBytes: long.MaxValue);

        Assert.IsFalse(File.Exists(old), "100일이 지난 파일은 지워져야 한다");
        Assert.IsTrue(File.Exists(recent), "기간 안의 파일은 남아야 한다");
    }

    [TestMethod]
    public void Clean_OverTotalCap_RemovesOldestFirst()
    {
        using var temp = new TempFolder();
        string oldest = Write(temp.Path, Today.AddDays(-3), 500);
        string middle = Write(temp.Path, Today.AddDays(-2), 500);
        string newest = Write(temp.Path, Today.AddDays(-1), 500);

        LogFileCleaner.Clean(temp.Path, Today, retentionDays: 100, maxTotalBytes: 1000);

        Assert.IsFalse(File.Exists(oldest), "상한을 넘으면 가장 오래된 것부터 지워야 한다");
        Assert.IsTrue(File.Exists(middle));
        Assert.IsTrue(File.Exists(newest));
    }

    [TestMethod]
    public void Clean_LeavesFilesThatAreNotLogs()
    {
        using var temp = new TempFolder();
        string stranger = Path.Combine(temp.Path, "app-settings.json");
        File.WriteAllText(stranger, "{}");

        LogFileCleaner.Clean(temp.Path, Today, retentionDays: 0, maxTotalBytes: 0);

        Assert.IsTrue(File.Exists(stranger), "기록 파일이 아닌 것은 건드리지 않아야 한다");
    }

    [TestMethod]
    public void Clean_MissingFolder_DoesNothingAndDoesNotThrow()
    {
        string missing = Path.Combine(Path.GetTempPath(), "aclog-none-" + Guid.NewGuid().ToString("N"));

        int removed = LogFileCleaner.Clean(missing, Today, retentionDays: 100, maxTotalBytes: 1000);

        Assert.AreEqual(0, removed);
    }

    private static string Write(string folder, DateOnly date, int bytes)
    {
        string path = Path.Combine(folder, LogFilePolicy.FileName(date, 0));
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aclog-clean-" + Guid.NewGuid().ToString("N"));

        public TempFolder() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { }
        }
    }
}
