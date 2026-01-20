using System;
using System.Collections.Generic;

namespace AnimatronicsControlCenter.Core.Transport;

/// <summary>
/// XBee API Frame Builder for API Mode 2 (Escaped)
/// Builds frames with proper escaping and checksum
/// </summary>
public class ApiFrameBuilder
{
    private byte _frameIdCounter = 1;

    /// <summary>
    /// Build a TX Request (0x10) frame
    /// </summary>
    public byte[] BuildTxRequest(TxRequestFrame request)
    {
        // Assign frame ID if not set
        if (request.FrameId == 0)
        {
            request.FrameId = GetNextFrameId();
        }

        // Frame data (before escaping):
        // [FrameType][FrameId][64-bit Dest][16-bit Dest][Radius][Options][RF Data...]
        int frameDataLen = 1 + 1 + 8 + 2 + 1 + 1 + request.RfData.Length;
        var frameData = new byte[frameDataLen];
        int idx = 0;

        frameData[idx++] = ApiFrameType.TxRequest;
        frameData[idx++] = request.FrameId;

        // 64-bit destination address (Big Endian)
        WriteUInt64BE(frameData, idx, request.DestAddress64);
        idx += 8;

        // 16-bit destination address (Big Endian)
        WriteUInt16BE(frameData, idx, request.DestAddress16);
        idx += 2;

        frameData[idx++] = request.BroadcastRadius;
        frameData[idx++] = request.Options;

        // RF Data payload
        Array.Copy(request.RfData, 0, frameData, idx, request.RfData.Length);

        return BuildFrame(frameData);
    }

    /// <summary>
    /// Build an AT Command (0x08) frame
    /// </summary>
    public byte[] BuildAtCommand(byte frameId, string command, byte[]? parameter = null)
    {
        if (frameId == 0)
            frameId = GetNextFrameId();

        int paramLen = parameter?.Length ?? 0;
        var frameData = new byte[1 + 1 + 2 + paramLen];
        int idx = 0;

        frameData[idx++] = ApiFrameType.AtCommand;
        frameData[idx++] = frameId;
        frameData[idx++] = (byte)command[0];
        frameData[idx++] = (byte)command[1];

        if (parameter != null)
        {
            Array.Copy(parameter, 0, frameData, idx, parameter.Length);
        }

        return BuildFrame(frameData);
    }

    /// <summary>
    /// Build complete frame with start delimiter, length, checksum, and escaping
    /// </summary>
    private byte[] BuildFrame(byte[] frameData)
    {
        // Calculate checksum (sum of frame data bytes, keep low 8 bits, subtract from 0xFF)
        byte checksum = 0;
        foreach (var b in frameData)
        {
            checksum += b;
        }
        checksum = (byte)(0xFF - checksum);

        // Build unescaped frame: [Start][LengthMSB][LengthLSB][FrameData][Checksum]
        var unescaped = new List<byte>
        {
            ApiConstants.StartDelimiter,
            (byte)(frameData.Length >> 8),
            (byte)(frameData.Length & 0xFF)
        };
        unescaped.AddRange(frameData);
        unescaped.Add(checksum);

        // Apply escaping (API Mode 2)
        return ApplyEscaping(unescaped);
    }

    /// <summary>
    /// Apply API Mode 2 escaping
    /// Escape: 0x7E, 0x7D, 0x11, 0x13 (except start delimiter)
    /// </summary>
    private byte[] ApplyEscaping(List<byte> data)
    {
        var escaped = new List<byte>();

        for (int i = 0; i < data.Count; i++)
        {
            byte b = data[i];

            // Don't escape the start delimiter (first byte)
            if (i == 0)
            {
                escaped.Add(b);
                continue;
            }

            if (NeedsEscape(b))
            {
                escaped.Add(ApiConstants.EscapeChar);
                escaped.Add((byte)(b ^ ApiConstants.EscapeXor));
            }
            else
            {
                escaped.Add(b);
            }
        }

        return escaped.ToArray();
    }

    /// <summary>
    /// Check if byte needs to be escaped
    /// </summary>
    private static bool NeedsEscape(byte b)
    {
        return b == ApiConstants.StartDelimiter ||
               b == ApiConstants.EscapeChar ||
               b == ApiConstants.Xon ||
               b == ApiConstants.Xoff;
    }

    /// <summary>
    /// Get next frame ID (1-255, wraps around)
    /// </summary>
    public byte GetNextFrameId()
    {
        byte id = _frameIdCounter++;
        if (_frameIdCounter == 0)
            _frameIdCounter = 1;
        return id;
    }

    private static void WriteUInt64BE(byte[] data, int offset, ulong value)
    {
        data[offset] = (byte)(value >> 56);
        data[offset + 1] = (byte)(value >> 48);
        data[offset + 2] = (byte)(value >> 40);
        data[offset + 3] = (byte)(value >> 32);
        data[offset + 4] = (byte)(value >> 24);
        data[offset + 5] = (byte)(value >> 16);
        data[offset + 6] = (byte)(value >> 8);
        data[offset + 7] = (byte)value;
    }

    private static void WriteUInt16BE(byte[] data, int offset, ushort value)
    {
        data[offset] = (byte)(value >> 8);
        data[offset + 1] = (byte)value;
    }
}
