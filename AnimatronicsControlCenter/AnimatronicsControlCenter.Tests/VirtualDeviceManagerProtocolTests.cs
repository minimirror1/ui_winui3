using System.Buffers.Binary;
using System.Text;
using AnimatronicsControlCenter.Core.Protocol;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class VirtualDeviceManagerProtocolTests
{
    [TestMethod]
    public void Virtual_SaveResponse_ReturnsPathConfirmationPayload()
    {
        var manager = new VirtualDeviceManager();
        byte[] request = BuildSaveRequest(0, 1, "Setting/MT_RP.TXT", "Value=2222");

        byte[]? response = manager.ProcessBinaryCommand(request);

        Assert.IsNotNull(response);
        Assert.IsTrue(BinaryDeserializer.TryParseResponseHeader(response, out var header));
        Assert.AreEqual(BinaryCommand.SaveFile, header.Cmd);
        Assert.AreEqual(ResponseStatus.Ok, header.Status);

        ReadOnlySpan<byte> payload = response.AsSpan(BinaryProtocolConst.ResponseHeaderSize, header.PayloadLen);
        Assert.AreEqual("Setting/MT_RP.TXT", BinaryDeserializer.ParseSaveFileResponse(payload));
    }

    [TestMethod]
    public void EvaluateSaveResponse_ReturnsConfirmedPath_WhenPathMatches()
    {
        byte[] response = BuildSaveResponse("Setting/MT_RP.TXT");

        SaveFileResponseProjection.SaveFileResponseResult result =
            SaveFileResponseProjection.Evaluate(response, "Setting/MT_RP.TXT");

        Assert.IsTrue(result.Success);
        Assert.AreEqual("Saved file: Setting/MT_RP.TXT", result.StatusMessage);
        Assert.AreEqual(string.Empty, result.ErrorDetail);
        Assert.AreEqual("Setting/MT_RP.TXT", result.ConfirmedPath);
    }

    [TestMethod]
    public void EvaluateSaveResponse_Fails_WhenReturnedPathDoesNotMatch()
    {
        byte[] response = BuildSaveResponse("Setting/OTHER.TXT");

        SaveFileResponseProjection.SaveFileResponseResult result =
            SaveFileResponseProjection.Evaluate(response, "Setting/MT_RP.TXT");

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Failed to save file: invalid device response.", result.StatusMessage);
        Assert.AreEqual("invalid device response.", result.ErrorDetail);
    }

    [TestMethod]
    public void EvaluateSaveResponse_UsesDeviceErrorMessage_WhenDeviceReturnsError()
    {
        byte[] response = BuildErrorResponse(BinaryCommand.SaveFile, BinaryErrorCode.FileNotFound, "File not found");

        SaveFileResponseProjection.SaveFileResponseResult result =
            SaveFileResponseProjection.Evaluate(response, "Setting/MT_RP.TXT");

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Failed to save file: File not found", result.StatusMessage);
        Assert.AreEqual("File not found", result.ErrorDetail);
    }

    private static byte[] BuildSaveRequest(byte srcId, byte tarId, string path, string content)
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
        request[2] = (byte)BinaryCommand.SaveFile;
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(3), (ushort)payload.Length);
        payload.CopyTo(request.AsSpan(BinaryProtocolConst.RequestHeaderSize));
        return request;
    }

    private static byte[] BuildSaveResponse(string path)
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(path);
        byte[] payload = new byte[2 + pathBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0), (ushort)pathBytes.Length);
        pathBytes.CopyTo(payload.AsSpan(2));

        byte[] response = new byte[BinaryProtocolConst.ResponseHeaderSize + payload.Length];
        response[0] = 1;
        response[1] = 0;
        response[2] = (byte)BinaryCommand.SaveFile;
        response[3] = (byte)ResponseStatus.Ok;
        BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(4), (ushort)payload.Length);
        payload.CopyTo(response.AsSpan(BinaryProtocolConst.ResponseHeaderSize));
        return response;
    }

    private static byte[] BuildErrorResponse(BinaryCommand cmd, BinaryErrorCode code, string message)
    {
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);
        byte[] payload = new byte[2 + messageBytes.Length];
        payload[0] = (byte)code;
        payload[1] = (byte)messageBytes.Length;
        messageBytes.CopyTo(payload.AsSpan(2));

        byte[] response = new byte[BinaryProtocolConst.ResponseHeaderSize + payload.Length];
        response[0] = 1;
        response[1] = 0;
        response[2] = (byte)cmd;
        response[3] = (byte)ResponseStatus.Error;
        BinaryPrimitives.WriteUInt16LittleEndian(response.AsSpan(4), (ushort)payload.Length);
        payload.CopyTo(response.AsSpan(BinaryProtocolConst.ResponseHeaderSize));
        return response;
    }
}
