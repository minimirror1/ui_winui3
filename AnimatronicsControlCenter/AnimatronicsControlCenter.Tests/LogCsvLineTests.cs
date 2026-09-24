using System;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests
{
    [TestClass]
    public class LogCsvLineTests
    {
        private static readonly DateTimeOffset At = new(2026, 9, 24, 9, 15, 0, 871, TimeSpan.FromHours(9));

        [TestMethod]
        public void Header_ListsColumnsInReadingOrder()
        {
            Assert.AreEqual("시각,방향,장치,명령,결과,길이,소요(ms),원문", LogCsvLine.Header);
        }

        [TestMethod]
        public void Format_FullRow_FillsEveryColumn()
        {
            var row = new LogRow(At, SerialTrafficDirection.Rx, 3, "Pong", "OK", 10, 43, "");

            Assert.AreEqual("09:15:00.871,RX,3,Pong,OK,10,43,", LogCsvLine.Format(row));
        }

        [TestMethod]
        public void Format_UnknownValues_LeavesThoseCellsEmpty()
        {
            var row = new LogRow(At, SerialTrafficDirection.Tx, null, "", "", null, null, "");

            Assert.AreEqual("09:15:00.871,TX,,,,,,", LogCsvLine.Format(row));
        }

        [TestMethod]
        public void Format_ValueContainingComma_IsQuoted()
        {
            var row = new LogRow(At, SerialTrafficDirection.Rx, 3, "Save,File", "OK", 1, 5, "");

            StringAssert.Contains(LogCsvLine.Format(row), "\"Save,File\"");
        }

        [TestMethod]
        public void Format_ValueContainingQuote_DoublesTheQuote()
        {
            var row = new LogRow(At, SerialTrafficDirection.Rx, 3, "Say\"Hi", "OK", 1, 5, "");

            StringAssert.Contains(LogCsvLine.Format(row), "\"Say\"\"Hi\"");
        }
    }
}
