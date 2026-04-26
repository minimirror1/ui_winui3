using System;
using System.Buffers.Binary;
using AnimatronicsControlCenter.Core.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BinaryProtocolCompatibilityTests
{
    [TestMethod]
    public void ParsePongResponse_ReadsStateInitAndMotionTimes()
    {
        byte[] payload = { 0x01, 0x03, 0x10, 0x27, 0x00, 0x00, 0x20, 0x4E, 0x00, 0x00 };

        PongStatus status = BinaryDeserializer.ParsePongResponse(payload);

        Assert.AreEqual(BinaryPingState.Playing, status.State);
        Assert.AreEqual((byte)0x03, status.InitState);
        Assert.AreEqual((uint)10000, status.CurrentMs);
        Assert.AreEqual((uint)20000, status.TotalMs);
    }

    [TestMethod]
    public void EncodePing_WithTimePayload_UsesFirmwareTimeShape()
    {
        var timestamp = new DateTimeOffset(2026, 4, 24, 15, 30, 45, TimeSpan.FromHours(9));

        byte[] packet = BinarySerializer.EncodePing(
            BinaryProtocolConst.HostId,
            tarId: 1,
            new PingTimePayload("kr", timestamp));

        Assert.AreEqual(BinaryProtocolConst.RequestHeaderSize + BinaryProtocolConst.PingTimePayloadSize, packet.Length);
        Assert.AreEqual((byte)BinaryProtocolConst.HostId, packet[0]);
        Assert.AreEqual((byte)1, packet[1]);
        Assert.AreEqual((byte)BinaryCommand.Ping, packet[2]);
        Assert.AreEqual((ushort)12, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(3)));

        byte[] expectedPayload =
        {
            0x01, 0x4B, 0x52, 0xEA, 0x07, 0x04, 0x18, 0x0F, 0x1E, 0x2D, 0x1C, 0x02
        };
        CollectionAssert.AreEqual(expectedPayload, packet.AsSpan(BinaryProtocolConst.RequestHeaderSize).ToArray());
    }

    [TestMethod]
    public void EncodePing_StillAllowsZeroLengthPayload()
    {
        byte[] packet = BinarySerializer.EncodePing(BinaryProtocolConst.HostId, tarId: 1);

        Assert.AreEqual(BinaryProtocolConst.RequestHeaderSize, packet.Length);
        Assert.AreEqual((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(3)));
    }

    [TestMethod]
    public void ParseErrorResponse_SupportsResponseTooLargeAndTxBusy()
    {
        byte[] responseTooLargePayload = BuildErrorPayload(0x06, "Response too large");
        byte[] txBusyPayload = BuildErrorPayload(0x07, "TX busy");

        var (responseTooLargeCode, responseTooLargeMessage) = BinaryDeserializer.ParseErrorResponse(responseTooLargePayload);
        var (txBusyCode, txBusyMessage) = BinaryDeserializer.ParseErrorResponse(txBusyPayload);

        Assert.AreEqual("ResponseTooLarge", responseTooLargeCode.ToString());
        Assert.AreEqual("Response too large", responseTooLargeMessage);
        Assert.AreEqual("TxBusy", txBusyCode.ToString());
        Assert.AreEqual("TX busy", txBusyMessage);
    }

    private static byte[] BuildErrorPayload(byte code, string message)
    {
        byte[] messageBytes = System.Text.Encoding.UTF8.GetBytes(message);
        byte[] payload = new byte[2 + messageBytes.Length];
        payload[0] = code;
        payload[1] = (byte)messageBytes.Length;
        messageBytes.CopyTo(payload.AsSpan(2));
        return payload;
    }
}
