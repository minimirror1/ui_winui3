using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests
{
    [TestClass]
    public class TrafficFileLoggerTests
    {
        private static readonly DateTimeOffset At = new(2026, 9, 24, 9, 15, 0, 871, TimeSpan.FromHours(9));

        [TestMethod]
        public void Record_WritesRowIntoFileNamedAfterTheEntryDate()
        {
            using var temp = new TempFolder();

            using (var logger = new TrafficFileLogger(temp.Path, Options()))
            {
                logger.Record(Entry(At));
            }

            string expected = Path.Combine(temp.Path, "2026-09-24.csv");
            Assert.IsTrue(File.Exists(expected), "날짜 이름의 기록 파일이 생겨야 한다");
            StringAssert.Contains(File.ReadAllText(expected), "09:15:00.871,TX");
        }

        [TestMethod]
        public void Record_NewFile_StartsWithHeaderRow()
        {
            using var temp = new TempFolder();

            using (var logger = new TrafficFileLogger(temp.Path, Options()))
            {
                logger.Record(Entry(At));
            }

            string[] lines = File.ReadAllLines(Path.Combine(temp.Path, "2026-09-24.csv"));
            Assert.AreEqual(LogCsvLine.Header, lines.First(l => !l.StartsWith('#')));
        }

        [TestMethod]
        public void Record_EntriesOnDifferentDays_GoToDifferentFiles()
        {
            using var temp = new TempFolder();

            using (var logger = new TrafficFileLogger(temp.Path, Options()))
            {
                logger.Record(Entry(At));
                logger.Record(Entry(At.AddDays(1)));
            }

            Assert.IsTrue(File.Exists(Path.Combine(temp.Path, "2026-09-24.csv")));
            Assert.IsTrue(File.Exists(Path.Combine(temp.Path, "2026-09-25.csv")));
        }

        [TestMethod]
        public void Record_PastSizeCap_RollsToNumberedFile()
        {
            using var temp = new TempFolder();

            using (var logger = new TrafficFileLogger(temp.Path, Options(maxFileBytes: 120)))
            {
                for (int i = 0; i < 20; i++) logger.Record(Entry(At));
            }

            Assert.IsTrue(File.Exists(Path.Combine(temp.Path, "2026-09-24.csv")));
            Assert.IsTrue(File.Exists(Path.Combine(temp.Path, "2026-09-24.1.csv")));
        }

        [TestMethod]
        public void Record_AfterRestart_AppendsToSameDayFileWithoutRepeatingHeader()
        {
            using var temp = new TempFolder();

            using (var logger = new TrafficFileLogger(temp.Path, Options()))
            {
                logger.Record(Entry(At));
            }

            using (var logger = new TrafficFileLogger(temp.Path, Options()))
            {
                logger.Record(Entry(At));
            }

            string[] lines = File.ReadAllLines(Path.Combine(temp.Path, "2026-09-24.csv"));
            Assert.AreEqual(1, lines.Count(l => l == LogCsvLine.Header), "머리글은 한 번만 나와야 한다");
            Assert.AreEqual(2, lines.Count(l => l.StartsWith("09:15:00.871")), "두 번 기록한 줄이 모두 남아야 한다");
        }

        [TestMethod]
        public void Record_QueueFull_DropsInsteadOfWaiting()
        {
            var gate = new ManualResetEventSlim(false);
            var logger = new TrafficFileLogger(new BlockedSink(gate), Options(queueCapacity: 2));

            try
            {
                for (int i = 0; i < 50; i++) logger.Record(Entry(At));

                Assert.IsTrue(logger.DroppedCount > 0, "대기줄이 차면 버린 개수가 세어져야 한다");
            }
            finally
            {
                gate.Set();
                logger.Dispose();
                gate.Dispose();
            }
        }

        [TestMethod]
        public void Record_AfterDropping_LeavesAGapMarkerInTheRecord()
        {
            var gate = new ManualResetEventSlim(false);
            var sink = new RecordingSink(gate);
            var logger = new TrafficFileLogger(sink, Options(queueCapacity: 2));

            for (int i = 0; i < 50; i++) logger.Record(Entry(At));

            gate.Set();
            logger.Dispose();
            gate.Dispose();

            Assert.IsTrue(
                sink.Lines.Any(l => l.StartsWith('#') && l.Contains("버림")),
                "버린 줄이 있으면 기록에 구멍 표시가 남아야 한다");
        }

        [TestMethod]
        public void Subscribe_RecordsWhatTheTrafficTapReports()
        {
            using var temp = new TempFolder();
            var tap = new SerialTrafficTap();

            using (var logger = new TrafficFileLogger(temp.Path, Options()))
            {
                logger.Subscribe(tap);
                tap.RecordTx("00 03 01 00 00");
                logger.Unsubscribe(tap);
            }

            string today = LogFilePolicy.FileName(DateOnly.FromDateTime(DateTime.Now), 0);
            StringAssert.Contains(File.ReadAllText(Path.Combine(temp.Path, today)), ",TX,3,Ping,");
        }

        [TestMethod]
        public void Record_WhileDisabled_WritesNothing()
        {
            using var temp = new TempFolder();

            using (var logger = new TrafficFileLogger(temp.Path, Options()))
            {
                logger.IsEnabled = false;
                logger.Record(Entry(At));
            }

            Assert.IsFalse(
                File.Exists(Path.Combine(temp.Path, "2026-09-24.csv")),
                "꺼져 있으면 파일이 생기지 않아야 한다");
        }

        [TestMethod]
        public void Record_WithRawTurnedOn_KeepsTheHexInTheRow()
        {
            using var temp = new TempFolder();

            using (var logger = new TrafficFileLogger(temp.Path, Options()))
            {
                logger.IncludeRaw = true;
                logger.Record(Entry(At));
            }

            StringAssert.Contains(
                File.ReadAllText(Path.Combine(temp.Path, "2026-09-24.csv")),
                "00 03 01 00 00");
        }

        /// 나간 줄을 모아두는 가짜 출력. 첫 줄에서 한 번 멈춰 대기줄이 차게 한다.
        private sealed class RecordingSink : ILogSink
        {
            private readonly ManualResetEventSlim _gate;
            private bool _waited;

            public RecordingSink(ManualResetEventSlim gate) => _gate = gate;

            public List<string> Lines { get; } = new();

            public void Write(DateOnly date, string line)
            {
                if (!_waited)
                {
                    _waited = true;
                    _gate.Wait();
                }

                Lines.Add(line);
            }

            public void Flush() { }

            public void Dispose() { }
        }

        /// 기록 스레드를 붙잡아 두는 가짜 출력. 대기줄이 차는 상황을 결정론적으로 만든다.
        private sealed class BlockedSink : ILogSink
        {
            private readonly ManualResetEventSlim _gate;

            public BlockedSink(ManualResetEventSlim gate) => _gate = gate;

            public void Write(DateOnly date, string line) => _gate.Wait();

            public void Flush() { }

            public void Dispose() { }
        }

        private static TrafficFileLoggerOptions Options(int queueCapacity)
            => new(MaxFileBytes: 50 * 1024 * 1024, queueCapacity, IncludeRaw: false);

        private static TrafficFileLoggerOptions Options(long maxFileBytes)
            => new(maxFileBytes, QueueCapacity: 10_000, IncludeRaw: false);

        private static TrafficFileLoggerOptions Options()
            => new(MaxFileBytes: 50 * 1024 * 1024, QueueCapacity: 10_000, IncludeRaw: false);

        private static SerialTrafficEntry Entry(DateTimeOffset at)
            => new(at, SerialTrafficDirection.Tx, "00 03 01 00 00");

        private sealed class TempFolder : IDisposable
        {
            public string Path { get; } =
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aclog-" + Guid.NewGuid().ToString("N"));

            public TempFolder() => Directory.CreateDirectory(Path);

            public void Dispose()
            {
                try { Directory.Delete(Path, recursive: true); } catch { }
            }
        }
    }
}
