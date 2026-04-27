using AnimatronicsControlCenter.Core.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class FirmwareProtocolLimitTests
{
    [DataTestMethod]
    [DataRow("A\nB", "A\r\nB")]
    [DataRow("A\rB", "A\r\nB")]
    [DataRow("A\r\nB", "A\r\nB")]
    [DataRow("A\nB\n", "A\r\nB\r\n")]
    public void NormalizeLineEndingsForDevice_ConvertsAllLineEndingsToCrLf(string input, string expected)
    {
        string result = FirmwareFileContentFormatting.NormalizeLineEndingsForDevice(input);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void SaveFilePayload_WithNormalizedContent_UsesOnlyCrLfLineEndings()
    {
        string contentForDevice = FirmwareFileContentFormatting.NormalizeLineEndingsForDevice("A\nB\rC\r\nD");

        byte[] packet = BinarySerializer.EncodeSaveFile(0, 1, "Setting/MT_ST.TXT", contentForDevice);
        byte[] content = ExtractPathContentPayloadContent(packet);

        CollectionAssert.Contains(content, (byte)0x0D);
        CollectionAssert.Contains(content, (byte)0x0A);
        Assert.AreEqual(3, CountCrLf(content));
        Assert.AreEqual(0, CountLoneCr(content));
        Assert.AreEqual(0, CountLoneLf(content));
    }

    [TestMethod]
    public void ValidateFileRequest_AcceptsPayloadsWithinFirmwareLimits()
    {
        string path = new('A', BinaryProtocolConst.MaxPathUtf8Bytes);
        string content = new('B', BinaryProtocolConst.MaxContentUtf8Bytes);

        var result = FirmwareFileRequestValidation.Validate(path, content);

        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(string.Empty, result.ErrorMessage);
    }

    [TestMethod]
    public void ValidateFileRequest_RejectsPathAtFirmwareBufferLimit()
    {
        string path = new('A', BinaryProtocolConst.AppPathMaxLen);

        var result = FirmwareFileRequestValidation.Validate(path, "ok");

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("file path exceeds firmware limit (127 UTF-8 bytes).", result.ErrorMessage);
    }

    [TestMethod]
    public void ValidateFileRequest_RejectsContentAtFirmwareBufferLimit()
    {
        string content = new('B', BinaryProtocolConst.AppContentMaxLen);

        var result = FirmwareFileRequestValidation.Validate("Setting/MT_RP.TXT", content);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual("file content exceeds firmware limit (511 UTF-8 bytes).", result.ErrorMessage);
    }

    [TestMethod]
    public void Describe_ReturnsReadableMessagesForFirmwareErrorCodes()
    {
        Assert.AreEqual("Device response was too large.", BinaryProtocolErrorText.Describe(BinaryErrorCode.ResponseTooLarge, BinaryCommand.SaveFile));
        Assert.AreEqual("Device transmitter is busy. Try again.", BinaryProtocolErrorText.Describe(BinaryErrorCode.TxBusy, BinaryCommand.SaveFile));
        Assert.AreEqual("Device rejected the request parameters.", BinaryProtocolErrorText.Describe(BinaryErrorCode.InvalidParam, BinaryCommand.VerifyFile));
    }

    private static byte[] ExtractPathContentPayloadContent(byte[] packet)
    {
        int payloadOffset = BinaryProtocolConst.RequestHeaderSize;
        ushort pathLen = BitConverter.ToUInt16(packet, payloadOffset);
        int contentLenOffset = payloadOffset + 2 + pathLen;
        ushort contentLen = BitConverter.ToUInt16(packet, contentLenOffset);
        int contentOffset = contentLenOffset + 2;
        byte[] content = new byte[contentLen];
        Array.Copy(packet, contentOffset, content, 0, contentLen);
        return content;
    }

    private static int CountCrLf(byte[] content)
    {
        int count = 0;
        for (int i = 0; i < content.Length - 1; i++)
        {
            if (content[i] == 0x0D && content[i + 1] == 0x0A) count++;
        }
        return count;
    }

    private static int CountLoneCr(byte[] content)
    {
        int count = 0;
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == 0x0D && (i + 1 >= content.Length || content[i + 1] != 0x0A)) count++;
        }
        return count;
    }

    private static int CountLoneLf(byte[] content)
    {
        int count = 0;
        for (int i = 0; i < content.Length; i++)
        {
            if (content[i] == 0x0A && (i == 0 || content[i - 1] != 0x0D)) count++;
        }
        return count;
    }
}
