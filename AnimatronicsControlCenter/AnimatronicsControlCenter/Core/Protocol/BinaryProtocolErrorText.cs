namespace AnimatronicsControlCenter.Core.Protocol;

public static class BinaryProtocolErrorText
{
    public static string Describe(BinaryErrorCode code, BinaryCommand cmd) => code switch
    {
        BinaryErrorCode.ResponseTooLarge => "Device response was too large.",
        BinaryErrorCode.TxBusy => "Device transmitter is busy. Try again.",
        BinaryErrorCode.InvalidParam => "Device rejected the request parameters.",
        BinaryErrorCode.InvalidInput => "Device reported invalid input.",
        BinaryErrorCode.FileNotFound => "File not found.",
        BinaryErrorCode.MotorNotFound => "Motor not found.",
        BinaryErrorCode.UnknownCmd => "Device did not recognize the command.",
        _ => $"Device returned {code} for {cmd}.",
    };
}
