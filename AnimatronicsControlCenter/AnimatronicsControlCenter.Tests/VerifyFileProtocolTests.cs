using System.Buffers.Binary;
using System.Text;
using AnimatronicsControlCenter.Core.Protocol;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class VerifyFileProtocolTests
{
    [TestMethod]
    public void ParseVerifyFileResponse_UsesTrailingMatchFlag()
    {
        byte[] payload = BuildVerifyResponsePayload("Setting/MT_RP.TXT", match: false);

        bool match = BinaryDeserializer.ParseVerifyFileResponse(payload);

        Assert.IsFalse(match);
    }

    [TestMethod]
    public void ParseVerifyFileResponse_ReturnsFalseForMalformedPayload()
    {
        byte[] payload = { 0x05, 0x00, (byte)'A', (byte)'B' };

        bool match = BinaryDeserializer.ParseVerifyFileResponse(payload);

        Assert.IsFalse(match);
    }

    [TestMethod]
    public void VirtualDeviceManager_VerifyResponse_IsFirmwareShaped()
    {
        var manager = new VirtualDeviceManager();
        byte[] request = BuildVerifyRequest(0, 1, "Setting/MT_RP.TXT", "Value=1000");

        byte[]? response = manager.ProcessBinaryCommand(request);

        Assert.IsNotNull(response);
        Assert.IsTrue(BinaryDeserializer.TryParseResponseHeader(response, out var header));
        Assert.AreEqual(BinaryCommand.VerifyFile, header.Cmd);
        Assert.AreEqual(ResponseStatus.Ok, header.Status);

        ReadOnlySpan<byte> payload = response.AsSpan(BinaryProtocolConst.ResponseHeaderSize, header.PayloadLen);
        byte[] expectedPath = Encoding.UTF8.GetBytes("Setting/MT_RP.TXT");

        Assert.AreEqual(2 + expectedPath.Length + 1, payload.Length);
        Assert.AreEqual((ushort)expectedPath.Length, BinaryPrimitives.ReadUInt16LittleEndian(payload));
        CollectionAssert.AreEqual(expectedPath, payload.Slice(2, expectedPath.Length).ToArray());
        Assert.AreEqual((byte)1, payload[^1]);
    }

    private static byte[] BuildVerifyResponsePayload(string path, bool match)
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(path);
        byte[] payload = new byte[2 + pathBytes.Length + 1];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, (ushort)pathBytes.Length);
        pathBytes.CopyTo(payload.AsSpan(2));
        payload[^1] = match ? (byte)1 : (byte)0;
        return payload;
    }

    private static byte[] BuildVerifyRequest(byte srcId, byte tarId, string path, string content)
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(path);
        byte[] contentBytes = Encoding.UTF8.GetBytes(content);
        byte[] payload = new byte[2 + pathBytes.Length + 2 + contentBytes.Length];
        int offset = 0;

        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset), (ushort)pathBytes.Length);
        offset += 2;
        pathBytes.CopyTo(payload.AsSpan(offset));
        offset += pathBytes.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset), (ushort)contentBytes.Length);
        offset += 2;
        contentBytes.CopyTo(payload.AsSpan(offset));

        byte[] request = new byte[BinaryProtocolConst.RequestHeaderSize + payload.Length];
        request[0] = srcId;
        request[1] = tarId;
        request[2] = (byte)BinaryCommand.VerifyFile;
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(3), (ushort)payload.Length);
        payload.CopyTo(request.AsSpan(BinaryProtocolConst.RequestHeaderSize));
        return request;
    }
}
