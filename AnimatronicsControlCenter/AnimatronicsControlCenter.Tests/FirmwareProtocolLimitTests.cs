using AnimatronicsControlCenter.Core.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class FirmwareProtocolLimitTests
{
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
}
