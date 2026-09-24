using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Protocol;
using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests
{
    [TestClass]
    public class LogRowBuilderTests
    {
        private static readonly DateTimeOffset At = new(2026, 9, 24, 9, 15, 0, 871, TimeSpan.FromHours(9));

        [TestMethod]
        public void FromEntry_Request_TakesDeviceFromTarget()
        {
            var entry = Tx(BinarySerializer.EncodePing(BinaryProtocolConst.HostId, tarId: 3));

            LogRow row = LogRowBuilder.FromEntry(entry, includeRaw: false);

            Assert.AreEqual(3, row.DeviceId);
            Assert.AreEqual("Ping", row.Command);
            Assert.AreEqual(string.Empty, row.Result);
        }

        [TestMethod]
        public void FromEntry_Response_TakesDeviceFromSourceAndFillsResult()
        {
            var entry = Rx(Response(srcId: 7, BinaryCommand.SaveFile, ResponseStatus.Error));

            LogRow row = LogRowBuilder.FromEntry(entry, includeRaw: false);

            Assert.AreEqual(7, row.DeviceId);
            Assert.AreEqual("SaveFile", row.Command);
            Assert.AreEqual("Error", row.Result);
        }

        [TestMethod]
        public void FromEntry_Undecodable_KeepsTimeAndLeavesColumnsEmpty()
        {
            var entry = new SerialTrafficEntry(At, SerialTrafficDirection.Rx, "zz zz");

            LogRow row = LogRowBuilder.FromEntry(entry, includeRaw: false);

            Assert.AreEqual(At, row.Timestamp);
            Assert.IsNull(row.DeviceId);
            Assert.AreEqual(string.Empty, row.Command);
        }

        [TestMethod]
        public void FromEntry_WithoutRaw_LeavesRawColumnEmpty()
        {
            var entry = Tx(BinarySerializer.EncodePing(BinaryProtocolConst.HostId, tarId: 3));

            LogRow row = LogRowBuilder.FromEntry(entry, includeRaw: false);

            Assert.AreEqual(string.Empty, row.Raw);
        }

        [TestMethod]
        public void FromEntry_WithRaw_KeepsTheHexAsRecorded()
        {
            var entry = Tx(BinarySerializer.EncodePing(BinaryProtocolConst.HostId, tarId: 3));

            LogRow row = LogRowBuilder.FromEntry(entry, includeRaw: true);

            Assert.AreEqual(entry.Line, row.Raw);
        }

        private static SerialTrafficEntry Tx(byte[] packet)
            => new(At, SerialTrafficDirection.Tx, Hex(packet));

        private static SerialTrafficEntry Rx(byte[] packet)
            => new(At, SerialTrafficDirection.Rx, Hex(packet));

        private static string Hex(byte[] data)
            => string.Join(" ", data.Select(b => b.ToString("X2")));

        /// 본문 없는 응답은 요청 헤더로도 읽히는 길이가 나올 수 있어, 실제와 같이 에러 본문을 실어 만든다.
        private static byte[] Response(byte srcId, BinaryCommand command, ResponseStatus status)
        {
            byte[] message = Encoding.UTF8.GetBytes("TX busy");
            byte[] payload = new byte[2 + message.Length];
            payload[0] = (byte)BinaryErrorCode.TxBusy;
            payload[1] = (byte)message.Length;
            message.CopyTo(payload.AsSpan(2));

            byte[] packet = new byte[BinaryProtocolConst.ResponseHeaderSize + payload.Length];
            packet[0] = srcId;
            packet[1] = BinaryProtocolConst.HostId;
            packet[2] = (byte)command;
            packet[3] = (byte)status;
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(4), (ushort)payload.Length);
            payload.CopyTo(packet.AsSpan(BinaryProtocolConst.ResponseHeaderSize));
            return packet;
        }
    }
}
