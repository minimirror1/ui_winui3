using System;
using System.Diagnostics;
using System.Threading;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests
{
    /// "로그가 통신 성능에 영향을 주지 않는다"를 지키는 검증.
    /// 주 기준은 시간 측정이 아니라 "기록 쪽이 아무리 느려도 통신 쪽은 기다리지 않는다"이다.
    [TestClass]
    public class TrafficFileLoggerPerformanceTests
    {
        private static readonly DateTimeOffset At = new(2026, 9, 24, 9, 15, 0, 871, TimeSpan.FromHours(9));

        [TestMethod]
        public void Record_WhileWritingIsSlow_DoesNotWaitForIt()
        {
            const int calls = 200;
            const int delayPerWriteMs = 50;

            // 기록 쪽이 한 줄에 50ms 씩 걸린다. 통신 쪽이 이걸 기다린다면 10초가 걸린다.
            var logger = new TrafficFileLogger(
                new SlowSink(delayPerWriteMs),
                new TrafficFileLoggerOptions(MaxFileBytes: long.MaxValue, QueueCapacity: 4, IncludeRaw: false));

            var watch = Stopwatch.StartNew();
            for (int i = 0; i < calls; i++) logger.Record(Entry());
            watch.Stop();

            int dropped = logger.DroppedCount;
            logger.Dispose();

            Assert.IsTrue(
                watch.ElapsedMilliseconds < 1000,
                $"통신 쪽은 기록을 기다리면 안 된다. 실제 {watch.ElapsedMilliseconds}ms");
            Assert.IsTrue(dropped > 0, "밀린 만큼은 버려져야 한다");
        }

        [TestMethod]
        public void Subscribing_AddsLittleCostToTheTrafficPath()
        {
            const int calls = 100_000;
            const int Warmup = 20_000;

            // 실측 0.05us 수준. 해독을 통신 스레드로 옮기면 2us 가 넘으므로, 이 예산이 그 실수를 잡는다.
            const double BudgetMicrosecondsPerCall = 0.5;

            var tap = new SerialTrafficTap(capacity: calls + 10);
            var logger = new TrafficFileLogger(
                new NullSink(),
                new TrafficFileLoggerOptions(MaxFileBytes: long.MaxValue, QueueCapacity: calls, IncludeRaw: false));

            // 두 경로 모두 한 번 돌려 둔다. 먼저 잰 쪽이 JIT 비용을 뒤집어쓰면 비교가 뒤집힌다.
            Measure(tap, Warmup);
            logger.Subscribe(tap);
            Measure(tap, Warmup);
            logger.Unsubscribe(tap);

            var withoutLogger = Measure(tap, calls);
            logger.Subscribe(tap);
            var withLogger = Measure(tap, calls);

            logger.Unsubscribe(tap);
            logger.Dispose();

            double addedPerCall = (withLogger.TotalMilliseconds - withoutLogger.TotalMilliseconds) * 1000 / calls;
            Assert.IsTrue(
                addedPerCall < BudgetMicrosecondsPerCall,
                $"로그를 켜서 늘어난 비용이 예산을 넘었다. 호출당 {addedPerCall:F2}us");
        }

        private static TimeSpan Measure(SerialTrafficTap tap, int calls)
        {
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < calls; i++) tap.RecordTx("00 03 01 00 00");
            watch.Stop();
            return watch.Elapsed;
        }

        private static SerialTrafficEntry Entry()
            => new(At, SerialTrafficDirection.Tx, "00 03 01 00 00");

        private sealed class SlowSink : ILogSink
        {
            private readonly int _delayMs;

            public SlowSink(int delayMs) => _delayMs = delayMs;

            public void Write(DateOnly date, string line) => Thread.Sleep(_delayMs);

            public void Flush() { }

            public void Dispose() { }
        }

        private sealed class NullSink : ILogSink
        {
            public void Write(DateOnly date, string line) { }

            public void Flush() { }

            public void Dispose() { }
        }
    }
}
