namespace AnimatronicsControlCenter.Core.Protocol;

public sealed record BinaryPacketDecodeResult(
    bool IsValid,
    bool IsResponse,
    string Command,
    string? Status,
    byte SrcId,
    byte TarId,
    ushort PayloadLength,
    string Summary,
    string Details,
    string RawHex,
    string? ParseError);
