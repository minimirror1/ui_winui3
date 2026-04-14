namespace AnimatronicsControlCenter.Core.Protocol;

// §3.1 Command (uint8)
public enum BinaryCommand : byte
{
    Ping          = 0x01,
    Pong          = 0x02,
    Move          = 0x03,
    MotionCtrl    = 0x04,
    GetMotors     = 0x10,
    GetMotorState = 0x11,
    GetFiles      = 0x20,
    GetFile       = 0x21,
    SaveFile      = 0x22,
    VerifyFile    = 0x23,
    Error         = 0xFF,
}

// §3.2 ResponseStatus (uint8)
public enum ResponseStatus : byte
{
    Ok    = 0x00,
    Error = 0x01,
}

// §3.3 MotorType (uint8)
public enum BinaryMotorType : byte
{
    Servo   = 0x00,
    DC      = 0x01,
    Stepper = 0x02,
}

// §3.4 MotorStatus (uint8)
public enum BinaryMotorStatus : byte
{
    Normal       = 0x00,
    Error        = 0x01,
    Overload     = 0x02,
    Disconnected = 0x03,
}

// §3.5 MotionAction (uint8)
public enum BinaryMotionAction : byte
{
    Play  = 0x00,
    Stop  = 0x01,
    Pause = 0x02,
    Seek  = 0x03,
}

// §3.6 ErrorCode (uint8)
public enum BinaryErrorCode : byte
{
    Unknown       = 0x00,
    InvalidInput  = 0x01,
    UnknownCmd    = 0x02,
    InvalidParam  = 0x03,
    FileNotFound  = 0x04,
    MotorNotFound = 0x05,
}

// 헤더 크기 상수
public static class BinaryProtocolConst
{
    public const int RequestHeaderSize  = 5;  // src(1)+tar(1)+cmd(1)+payload_len(2)
    public const int ResponseHeaderSize = 6;  // src(1)+tar(1)+cmd(1)+status(1)+payload_len(2)
    public const byte HostId            = 0;
    public const byte BroadcastId       = 0xFF;
}

// 요청 헤더 (parsed)
public record struct RequestHeader(byte SrcId, byte TarId, BinaryCommand Cmd, ushort PayloadLen);

// 응답 헤더 (parsed)
public record struct ResponseHeader(byte SrcId, byte TarId, BinaryCommand Cmd, ResponseStatus Status, ushort PayloadLen);
