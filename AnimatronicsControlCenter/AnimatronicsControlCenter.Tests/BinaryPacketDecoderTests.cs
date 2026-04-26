using System.Buffers.Binary;
using System.Text;
using AnimatronicsControlCenter.Core.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BinaryPacketDecoderTests
{
    [TestMethod]
    public void DecodePongResponse_IncludesStateAndTimes()
    {
        byte[] payload = { 0x01, 0x03, 0x39, 0x30, 0x00, 0x00, 0x60, 0xEA, 0x00, 0x00 };
        byte[] packet = BuildResponse(BinaryCommand.Pong, ResponseStatus.Ok, payload);

        BinaryPacketDecodeResult result = BinaryPacketDecoder.Decode(packet);

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsResponse);
        Assert.AreEqual("Pong", result.Command);
        Assert.AreEqual("Ok", result.Status);
        StringAssert.Contains(result.Summary, "PONG OK");
        StringAssert.Contains(result.Details, "state=Playing");
        StringAssert.Contains(result.Details, "init_state=3");
        StringAssert.Contains(result.Details, "current_ms=12345");
        StringAssert.Contains(result.Details, "total_ms=60000");
    }

    [TestMethod]
    public void DecodeErrorResponse_IncludesErrorCodeAndMessage()
    {
        byte[] payload = BuildErrorPayload(BinaryErrorCode.TxBusy, "TX busy");
        byte[] packet = BuildResponse(BinaryCommand.SaveFile, ResponseStatus.Error, payload);

        BinaryPacketDecodeResult result = BinaryPacketDecoder.Decode(packet);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual("SaveFile", result.Command);
        Assert.AreEqual("Error", result.Status);
        StringAssert.Contains(result.Summary, "SaveFile ERROR");
        StringAssert.Contains(result.Details, "error_code=TxBusy");
        StringAssert.Contains(result.Details, "message=TX busy");
        Assert.IsNull(result.ParseError);
    }

    [TestMethod]
    public void DecodeSaveFileResponse_IncludesReturnedPath()
    {
        byte[] packet = BuildResponse(BinaryCommand.SaveFile, ResponseStatus.Ok, BuildPathPayload("Setting/MT_RP.TXT"));

        BinaryPacketDecodeResult result = BinaryPacketDecoder.Decode(packet);

        Assert.IsTrue(result.IsValid);
        StringAssert.Contains(result.Summary, "SAVE_FILE OK");
        StringAssert.Contains(result.Details, "path=Setting/MT_RP.TXT");
    }

    [TestMethod]
    public void DecodeVerifyFileResponse_IncludesPathAndMatch()
    {
        byte[] payload = BuildVerifyPayload("Setting/MT_RP.TXT", match: false);
        byte[] packet = BuildResponse(BinaryCommand.VerifyFile, ResponseStatus.Ok, payload);

        BinaryPacketDecodeResult result = BinaryPacketDecoder.Decode(packet);

        Assert.IsTrue(result.IsValid);
        StringAssert.Contains(result.Summary, "VERIFY_FILE OK");
        StringAssert.Contains(result.Details, "path=Setting/MT_RP.TXT");
        StringAssert.Contains(result.Details, "match=False");
    }

    [TestMethod]
    public void DecodePacket_ReportsPayloadLengthMismatch()
    {
        byte[] packet = BuildResponse(BinaryCommand.Pong, ResponseStatus.Ok, new byte[] { 0x01, 0x03 });
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(4), 10);

        BinaryPacketDecodeResult result = BinaryPacketDecoder.Decode(packet);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("payload length mismatch: header=10 actual=2", result.ParseError);
        StringAssert.Contains(result.Details, "raw=");
    }

    private static byte[] BuildResponse(BinaryCommand command, ResponseStatus status, byte[] payload)
    {
        byte[] packet = new byte[BinaryProtocolConst.ResponseHeaderSize + payload.Length];
        packet[0] = 1;
        packet[1] = 0;
        packet[2] = (byte)command;
        packet[3] = (byte)status;
        BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(4), (ushort)payload.Length);
        payload.CopyTo(packet.AsSpan(BinaryProtocolConst.ResponseHeaderSize));
        return packet;
    }

    private static byte[] BuildPathPayload(string path)
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(path);
        byte[] payload = new byte[2 + pathBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)pathBytes.Length);
        pathBytes.CopyTo(payload.AsSpan(2));
        return payload;
    }

    private static byte[] BuildVerifyPayload(string path, bool match)
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(path);
        byte[] payload = new byte[2 + pathBytes.Length + 1];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)pathBytes.Length);
        pathBytes.CopyTo(payload.AsSpan(2));
        payload[^1] = match ? (byte)1 : (byte)0;
        return payload;
    }

    private static byte[] BuildErrorPayload(BinaryErrorCode code, string message)
    {
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        byte[] payload = new byte[2 + messageBytes.Length];
        payload[0] = (byte)code;
        payload[1] = (byte)messageBytes.Length;
        messageBytes.CopyTo(payload.AsSpan(2));
        return payload;
    }
}
