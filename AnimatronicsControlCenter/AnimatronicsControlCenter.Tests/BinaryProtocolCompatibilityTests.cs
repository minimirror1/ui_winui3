using System;
using System.Buffers.Binary;
using AnimatronicsControlCenter.Core.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BinaryProtocolCompatibilityTests
{
    [TestMethod]
    public void MotorTypeWireValues_MatchFirmwareEnum()
    {
        Assert.AreEqual((byte)0x00, (byte)BinaryMotorType.Null);
        Assert.AreEqual((byte)0x01, (byte)BinaryMotorType.RC);
        Assert.AreEqual((byte)0x02, (byte)BinaryMotorType.AC);
        Assert.AreEqual((byte)0x03, (byte)BinaryMotorType.BL);
        Assert.AreEqual((byte)0x04, (byte)BinaryMotorType.ZER);
        Assert.AreEqual((byte)0x05, (byte)BinaryMotorType.DXL);
        Assert.AreEqual((byte)0x06, (byte)BinaryMotorType.AC2);
    }

    [TestMethod]
    public void DecodeAndEncodeMotorType_UseFirmwareNames()
    {
        Assert.AreEqual("Null", BinaryDeserializer.DecodeMotorType(0x00));
        Assert.AreEqual("RC", BinaryDeserializer.DecodeMotorType(0x01));
        Assert.AreEqual("AC", BinaryDeserializer.DecodeMotorType(0x02));
        Assert.AreEqual("BL", BinaryDeserializer.DecodeMotorType(0x03));
        Assert.AreEqual("ZER", BinaryDeserializer.DecodeMotorType(0x04));
        Assert.AreEqual("DXL", BinaryDeserializer.DecodeMotorType(0x05));
        Assert.AreEqual("AC2", BinaryDeserializer.DecodeMotorType(0x06));
        Assert.AreEqual("Unknown", BinaryDeserializer.DecodeMotorType(0x7F));

        Assert.AreEqual((byte)0x00, BinaryDeserializer.EncodeMotorType("Null"));
        Assert.AreEqual((byte)0x01, BinaryDeserializer.EncodeMotorType("RC"));
        Assert.AreEqual((byte)0x02, BinaryDeserializer.EncodeMotorType("AC"));
        Assert.AreEqual((byte)0x03, BinaryDeserializer.EncodeMotorType("BL"));
        Assert.AreEqual((byte)0x04, BinaryDeserializer.EncodeMotorType("ZER"));
        Assert.AreEqual((byte)0x05, BinaryDeserializer.EncodeMotorType("DXL"));
        Assert.AreEqual((byte)0x06, BinaryDeserializer.EncodeMotorType("AC2"));
    }

    [TestMethod]
    public void ParseGetMotorsResponse_DecodesFirmwareMotorType()
    {
        byte[] payload =
        {
            0x01,
            0x01, 0x02, 0x03, 0x05, 0x00,
            0x00, 0x08,
            0x64, 0x00,
            0x00, 0x00,
            0x08, 0x07,
            0x00, 0x00,
            0xFF, 0x0F
        };

        var patches = BinaryDeserializer.ParseGetMotorsResponse(payload);

        Assert.AreEqual(1, patches.Count);
        Assert.AreEqual("DXL", patches[0].Type);
    }

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
