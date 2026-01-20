namespace AnimatronicsControlCenter.Core.Transport;

/// <summary>
/// XBee API Frame Type identifiers (API Mode 2 - Escaped)
/// Reference: XBee S2C ZigBee RF Module User Guide
/// </summary>
public static class ApiFrameType
{
    // Transmit frames (Host -> XBee)
    public const byte TxRequest = 0x10;           // Transmit Request
    public const byte ExplicitTxRequest = 0x11;   // Explicit Addressing Command Frame
    public const byte AtCommand = 0x08;           // AT Command
    public const byte AtCommandQueue = 0x09;      // AT Command - Queue Parameter Value
    public const byte RemoteAtCommand = 0x17;     // Remote AT Command Request

    // Receive frames (XBee -> Host)
    public const byte RxPacket = 0x90;            // Receive Packet (AO=0)
    public const byte ExplicitRxIndicator = 0x91; // Explicit Rx Indicator (AO=1)
    public const byte TxStatus = 0x8B;            // Transmit Status
    public const byte AtCommandResponse = 0x88;  // AT Command Response
    public const byte ModemStatus = 0x8A;         // Modem Status
    public const byte RemoteAtResponse = 0x97;    // Remote AT Command Response
}

/// <summary>
/// XBee API Mode constants
/// </summary>
public static class ApiConstants
{
    public const byte StartDelimiter = 0x7E;
    public const byte EscapeChar = 0x7D;
    public const byte Xon = 0x11;
    public const byte Xoff = 0x13;
    public const byte EscapeXor = 0x20;

    // Broadcast addresses
    public const ulong BroadcastAddress64 = 0x000000000000FFFF;
    public const ushort BroadcastAddress16 = 0xFFFE;
    public const ushort UnknownAddress16 = 0xFFFE;

    // Default options
    public const byte DefaultBroadcastRadius = 0x00;
    public const byte DefaultTransmitOptions = 0x00;
}

/// <summary>
/// Transmit Status delivery status codes
/// </summary>
public enum DeliveryStatus : byte
{
    Success = 0x00,
    MacAckFailure = 0x01,
    CcaFailure = 0x02,
    InvalidDestinationEndpoint = 0x15,
    NetworkAckFailure = 0x21,
    NotJoinedToNetwork = 0x22,
    SelfAddressed = 0x23,
    AddressNotFound = 0x24,
    RouteNotFound = 0x25,
    BroadcastSourceFailed = 0x26,
    InvalidBindingTableIndex = 0x2B,
    ResourceError = 0x2C,
    AttemptedBroadcast = 0x2D,
    AttemptedUnicast = 0x2E,
    ResourceError2 = 0x32,
    DataPayloadTooLarge = 0x74,
    IndirectMessageUnrequested = 0x75
}

/// <summary>
/// Parsed TX Request frame (0x10)
/// </summary>
public class TxRequestFrame
{
    public byte FrameId { get; set; }
    public ulong DestAddress64 { get; set; }
    public ushort DestAddress16 { get; set; } = ApiConstants.UnknownAddress16;
    public byte BroadcastRadius { get; set; } = ApiConstants.DefaultBroadcastRadius;
    public byte Options { get; set; } = ApiConstants.DefaultTransmitOptions;
    public byte[] RfData { get; set; } = [];
}

/// <summary>
/// Parsed RX Packet frame (0x90)
/// </summary>
public class RxPacketFrame
{
    public ulong SourceAddress64 { get; set; }
    public ushort SourceAddress16 { get; set; }
    public byte ReceiveOptions { get; set; }
    public byte[] RfData { get; set; } = [];
}

/// <summary>
/// Parsed Explicit RX Indicator frame (0x91)
/// Used in DigiMesh and ZigBee for endpoint addressing
/// </summary>
public class ExplicitRxFrame
{
    public ulong SourceAddress64 { get; set; }
    public ushort SourceAddress16 { get; set; }
    public byte SourceEndpoint { get; set; }
    public byte DestEndpoint { get; set; }
    public ushort ClusterId { get; set; }
    public ushort ProfileId { get; set; }
    public byte ReceiveOptions { get; set; }
    public byte[] RfData { get; set; } = [];
}

/// <summary>
/// Parsed TX Status frame (0x8B)
/// </summary>
public class TxStatusFrame
{
    public byte FrameId { get; set; }
    public ushort DestAddress16 { get; set; }
    public byte TransmitRetryCount { get; set; }
    public DeliveryStatus DeliveryStatus { get; set; }
    public byte DiscoveryStatus { get; set; }
}

/// <summary>
/// Parsed AT Command Response frame (0x88)
/// </summary>
public class AtCommandResponseFrame
{
    public byte FrameId { get; set; }
    public string Command { get; set; } = "";
    public byte Status { get; set; }
    public byte[] Data { get; set; } = [];

    public bool IsSuccess => Status == 0;
}

/// <summary>
/// Generic API frame wrapper
/// </summary>
public class ApiFrame
{
    public byte FrameType { get; set; }
    public byte[] Data { get; set; } = [];

    // Parsed frame objects (only one will be set based on FrameType)
    public RxPacketFrame? RxPacket { get; set; }
    public ExplicitRxFrame? ExplicitRx { get; set; }
    public TxStatusFrame? TxStatus { get; set; }
    public AtCommandResponseFrame? AtResponse { get; set; }
}
