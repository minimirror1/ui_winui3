using System;
using System.IO;
using System.Linq;
using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests
{
    [TestClass]
    public class LogFilePolicyTests
    {
        [TestMethod]
        public void FileName_ForDate_IsIsoDateWithCsvExtension()
        {
            string name = LogFilePolicy.FileName(new DateOnly(2026, 9, 24), 0);

            Assert.AreEqual("2026-09-24.csv", name);
        }

        [TestMethod]
        public void FileName_WithSequence_PutsNumberBeforeExtension()
        {
            string name = LogFilePolicy.FileName(new DateOnly(2026, 9, 24), 2);

            Assert.AreEqual("2026-09-24.2.csv", name);
        }

        [TestMethod]
        public void TryParseDate_PlainName_ReturnsThatDate()
        {
            bool ok = LogFilePolicy.TryParseDate("2026-09-24.csv", out DateOnly date);

            Assert.IsTrue(ok);
            Assert.AreEqual(new DateOnly(2026, 9, 24), date);
        }

        [TestMethod]
        public void TryParseDate_NumberedName_ReturnsSameDateAsPlainName()
        {
            bool ok = LogFilePolicy.TryParseDate("2026-09-24.7.csv", out DateOnly date);

            Assert.IsTrue(ok);
            Assert.AreEqual(new DateOnly(2026, 9, 24), date);
        }

        [TestMethod]
        public void TryParseDate_UnrelatedFile_IsRejected()
        {
            bool ok = LogFilePolicy.TryParseDate("app-settings.json", out _);

            Assert.IsFalse(ok);
        }

        [TestMethod]
        public void SelectExpired_KeepsFileExactlyAtRetentionBoundary()
        {
            DateOnly today = new(2026, 9, 24);
            string boundary = LogFilePolicy.FileName(today.AddDays(-100), 0);

            var expired = LogFilePolicy.SelectExpired(new[] { boundary }, today, 100);

            CollectionAssert.AreEqual(Array.Empty<string>(), expired.ToArray());
        }

        [TestMethod]
        public void SelectExpired_ReturnsFileOlderThanRetention()
        {
            DateOnly today = new(2026, 9, 24);
            string tooOld = LogFilePolicy.FileName(today.AddDays(-101), 0);

            var expired = LogFilePolicy.SelectExpired(new[] { tooOld }, today, 100);

            CollectionAssert.AreEqual(new[] { tooOld }, expired.ToArray());
        }

        [TestMethod]
        public void SelectExpired_IgnoresFilesThatAreNotLogs()
        {
            DateOnly today = new(2026, 9, 24);

            var expired = LogFilePolicy.SelectExpired(new[] { "app-settings.json" }, today, 100);

            CollectionAssert.AreEqual(Array.Empty<string>(), expired.ToArray());
        }

        [TestMethod]
        public void FolderFor_PutsLogsBesideTheSettingsFile()
        {
            string folder = LogFilePolicy.FolderFor(Path.Combine("C:", "data", "app-settings.json"));

            Assert.AreEqual(Path.Combine("C:", "data", "Logs"), folder);
        }

        [TestMethod]
        public void FolderFor_PathWithoutDirectory_FallsBackToPlainFolderName()
        {
            string folder = LogFilePolicy.FolderFor("app-settings.json");

            Assert.AreEqual("Logs", folder);
        }

        [TestMethod]
        public void SelectOverCap_TotalWithinCap_ReturnsNothing()
        {
            var files = new[]
            {
                new LogFileEntry("2026-09-23.csv", 400),
                new LogFileEntry("2026-09-24.csv", 500),
            };

            var doomed = LogFilePolicy.SelectOverCap(files, 1000);

            CollectionAssert.AreEqual(Array.Empty<string>(), doomed.ToArray());
        }

        [TestMethod]
        public void SelectOverCap_OverCap_RemovesOldestUntilWithinCap()
        {
            var files = new[]
            {
                new LogFileEntry("2026-09-24.csv", 500),
                new LogFileEntry("2026-09-22.csv", 500),
                new LogFileEntry("2026-09-23.csv", 500),
            };

            var doomed = LogFilePolicy.SelectOverCap(files, 1000);

            CollectionAssert.AreEqual(new[] { "2026-09-22.csv" }, doomed.ToArray());
        }

        [TestMethod]
        public void SelectOverCap_SameDay_TreatsPlainNameAsOlderThanNumbered()
        {
            var files = new[]
            {
                new LogFileEntry("2026-09-24.1.csv", 500),
                new LogFileEntry("2026-09-24.csv", 500),
            };

            var doomed = LogFilePolicy.SelectOverCap(files, 500);

            CollectionAssert.AreEqual(new[] { "2026-09-24.csv" }, doomed.ToArray());
        }

        [TestMethod]
        public void SelectOverCap_NeverRemovesTheNewestFile()
        {
            var files = new[] { new LogFileEntry("2026-09-24.csv", 9999) };

            var doomed = LogFilePolicy.SelectOverCap(files, 100);

            CollectionAssert.AreEqual(Array.Empty<string>(), doomed.ToArray());
        }
    }
}
