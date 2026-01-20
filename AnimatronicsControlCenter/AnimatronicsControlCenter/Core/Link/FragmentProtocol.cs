using System;

namespace AnimatronicsControlCenter.Core.Link;

/// <summary>
/// Fragment protocol constants and structures
/// </summary>
public static class FragmentProtocol
{
    /// <summary>
    /// Protocol version
    /// </summary>
    public const byte Version = 0x01;

    /// <summary>
    /// Default payload size per fragment (excluding header)
    /// DigiMesh with encryption: NP=49, Header: 13B, CRC: 2B → Max payload: 34B
    /// Safe default: 30B (leaves 4B margin)
    /// </summary>
    public const int MaxPayloadSize = 30;

    /// <summary>
    /// Header size (without CRC)
    /// ver(1) + type(1) + msg_id(2) + total_len(4) + frag_idx(2) + frag_cnt(2) + payload_len(1) = 13
    /// </summary>
    public const int HeaderSize = 13;

    /// <summary>
    /// CRC size
    /// </summary>
    public const int CrcSize = 2;

    /// <summary>
    /// Maximum total message size (10KB)
    /// </summary>
    public const int MaxMessageSize = 10 * 1024;

    /// <summary>
    /// Timing parameters (milliseconds)
    /// Adjusted for larger messages with many fragments
    /// </summary>
    public const int FragmentTimeoutMs = 500;      // Increased from 150ms for reliability
    public const int SessionTimeoutMs = 30000;     // Increased from 5s to 30s for large messages
    public const int NackIntervalMs = 200;         // Increased from 100ms
    public const int MaxNackRounds = 10;           // Increased from 5 for reliability
}

/// <summary>
/// Fragment message types
/// </summary>
public enum FragmentType : byte
{
    Data = 0x01,
    Nack = 0x02,
    Done = 0x03
}

/// <summary>
/// Fragment header structure
/// </summary>
public struct FragmentHeader
{
    public byte Version;
    public FragmentType Type;
    public ushort MsgId;
    public uint TotalLen;
    public ushort FragIdx;
    public ushort FragCnt;
    public byte PayloadLen;

    /// <summary>
    /// Serialize header to bytes
    /// </summary>
    public readonly void WriteTo(byte[] buffer, int offset)
    {
        buffer[offset] = Version;
        buffer[offset + 1] = (byte)Type;
        buffer[offset + 2] = (byte)(MsgId >> 8);
        buffer[offset + 3] = (byte)(MsgId & 0xFF);
        buffer[offset + 4] = (byte)(TotalLen >> 24);
        buffer[offset + 5] = (byte)(TotalLen >> 16);
        buffer[offset + 6] = (byte)(TotalLen >> 8);
        buffer[offset + 7] = (byte)(TotalLen & 0xFF);
        buffer[offset + 8] = (byte)(FragIdx >> 8);
        buffer[offset + 9] = (byte)(FragIdx & 0xFF);
        buffer[offset + 10] = (byte)(FragCnt >> 8);
        buffer[offset + 11] = (byte)(FragCnt & 0xFF);
        buffer[offset + 12] = PayloadLen;
    }

    /// <summary>
    /// Parse header from bytes
    /// </summary>
    public static FragmentHeader ReadFrom(byte[] buffer, int offset)
    {
        return new FragmentHeader
        {
            Version = buffer[offset],
            Type = (FragmentType)buffer[offset + 1],
            MsgId = (ushort)((buffer[offset + 2] << 8) | buffer[offset + 3]),
            TotalLen = ((uint)buffer[offset + 4] << 24) |
                       ((uint)buffer[offset + 5] << 16) |
                       ((uint)buffer[offset + 6] << 8) |
                       buffer[offset + 7],
            FragIdx = (ushort)((buffer[offset + 8] << 8) | buffer[offset + 9]),
            FragCnt = (ushort)((buffer[offset + 10] << 8) | buffer[offset + 11]),
            PayloadLen = buffer[offset + 12]
        };
    }

    /// <summary>
    /// Total RF payload size (header + payload + CRC)
    /// </summary>
    public readonly int TotalSize => FragmentProtocol.HeaderSize + PayloadLen + FragmentProtocol.CrcSize;
}

/// <summary>
/// NACK message structure
/// </summary>
public class NackMessage
{
    public ushort MsgId { get; set; }
    public ushort[] MissingIndices { get; set; } = [];

    /// <summary>
    /// Build NACK RF payload
    /// Format: ver(1) + type(1) + msg_id(2) + count(1) + indices(N*2) + crc(2)
    /// </summary>
    public byte[] ToBytes()
    {
        int size = 5 + MissingIndices.Length * 2 + 2;
        var buffer = new byte[size];

        buffer[0] = FragmentProtocol.Version;
        buffer[1] = (byte)FragmentType.Nack;
        buffer[2] = (byte)(MsgId >> 8);
        buffer[3] = (byte)(MsgId & 0xFF);
        buffer[4] = (byte)MissingIndices.Length;

        int offset = 5;
        foreach (var idx in MissingIndices)
        {
            buffer[offset++] = (byte)(idx >> 8);
            buffer[offset++] = (byte)(idx & 0xFF);
        }

        // Append CRC
        Crc16.Append(buffer, 0, size - 2);

        return buffer;
    }

    /// <summary>
    /// Parse NACK from RF payload
    /// </summary>
    public static NackMessage? FromBytes(byte[] data, int offset, int length)
    {
        if (length < 7) return null;
        if (!Crc16.Verify(data, offset, length)) return null;
        if (data[offset] != FragmentProtocol.Version) return null;
        if (data[offset + 1] != (byte)FragmentType.Nack) return null;

        var nack = new NackMessage
        {
            MsgId = (ushort)((data[offset + 2] << 8) | data[offset + 3])
        };

        int count = data[offset + 4];
        if (length < 5 + count * 2 + 2) return null;

        nack.MissingIndices = new ushort[count];
        int idx = offset + 5;
        for (int i = 0; i < count; i++)
        {
            nack.MissingIndices[i] = (ushort)((data[idx] << 8) | data[idx + 1]);
            idx += 2;
        }

        return nack;
    }
}

/// <summary>
/// DONE message structure
/// </summary>
public static class DoneMessage
{
    /// <summary>
    /// Build DONE RF payload
    /// Format: ver(1) + type(1) + msg_id(2) + crc(2)
    /// </summary>
    public static byte[] ToBytes(ushort msgId)
    {
        var buffer = new byte[6];
        buffer[0] = FragmentProtocol.Version;
        buffer[1] = (byte)FragmentType.Done;
        buffer[2] = (byte)(msgId >> 8);
        buffer[3] = (byte)(msgId & 0xFF);
        Crc16.Append(buffer, 0, 4);
        return buffer;
    }

    /// <summary>
    /// Parse DONE from RF payload
    /// </summary>
    public static ushort? FromBytes(byte[] data, int offset, int length)
    {
        if (length != 6) return null;
        if (!Crc16.Verify(data, offset, length)) return null;
        if (data[offset] != FragmentProtocol.Version) return null;
        if (data[offset + 1] != (byte)FragmentType.Done) return null;

        return (ushort)((data[offset + 2] << 8) | data[offset + 3]);
    }
}
