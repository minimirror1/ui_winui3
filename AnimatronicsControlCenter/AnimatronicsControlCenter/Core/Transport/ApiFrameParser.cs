using System;

namespace AnimatronicsControlCenter.Core.Transport;

/// <summary>
/// XBee API Frame Parser for API Mode 2 (Escaped)
/// Handles incoming bytes and emits complete frames
/// </summary>
public class ApiFrameParser
{
    private enum ParseState
    {
        WaitingForStart,
        LengthMsb,
        LengthLsb,
        FrameData,
        Checksum
    }

    private ParseState _state = ParseState.WaitingForStart;
    private bool _escaped = false;
    private int _frameLength;
    private int _dataIndex;
    private byte[] _frameBuffer = new byte[256];
    private byte _checksum;

    /// <summary>
    /// Event raised when a complete frame is parsed
    /// </summary>
    public event Action<ApiFrame>? OnFrameReceived;

    /// <summary>
    /// Event raised on parse error
    /// </summary>
    public event Action<string>? OnParseError;

    /// <summary>
    /// Process incoming bytes from serial port
    /// </summary>
    public void ProcessBytes(byte[] data, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            ProcessByte(data[offset + i]);
        }
    }

    /// <summary>
    /// Process a single byte
    /// </summary>
    private void ProcessByte(byte b)
    {
        // Handle escape sequence (API Mode 2)
        if (_escaped)
        {
            b = (byte)(b ^ ApiConstants.EscapeXor);
            _escaped = false;
        }
        else if (b == ApiConstants.EscapeChar && _state != ParseState.WaitingForStart)
        {
            _escaped = true;
            return;
        }

        switch (_state)
        {
            case ParseState.WaitingForStart:
                if (b == ApiConstants.StartDelimiter)
                {
                    _state = ParseState.LengthMsb;
                    _checksum = 0;
                    _dataIndex = 0;
                }
                break;

            case ParseState.LengthMsb:
                _frameLength = b << 8;
                _state = ParseState.LengthLsb;
                break;

            case ParseState.LengthLsb:
                _frameLength |= b;
                if (_frameLength > 256 || _frameLength == 0)
                {
                    OnParseError?.Invoke($"Invalid frame length: {_frameLength}");
                    Reset();
                }
                else
                {
                    _frameBuffer = new byte[_frameLength];
                    _state = ParseState.FrameData;
                }
                break;

            case ParseState.FrameData:
                _frameBuffer[_dataIndex++] = b;
                _checksum += b;
                if (_dataIndex >= _frameLength)
                {
                    _state = ParseState.Checksum;
                }
                break;

            case ParseState.Checksum:
                _checksum += b;
                if (_checksum == 0xFF)
                {
                    // Valid frame - parse and emit
                    var frame = ParseFrame(_frameBuffer, _frameLength);
                    if (frame != null)
                    {
                        OnFrameReceived?.Invoke(frame);
                    }
                }
                else
                {
                    OnParseError?.Invoke($"Checksum error: expected 0xFF, got 0x{_checksum:X2}");
                }
                Reset();
                break;
        }
    }

    /// <summary>
    /// Reset parser state
    /// </summary>
    public void Reset()
    {
        _state = ParseState.WaitingForStart;
        _escaped = false;
        _dataIndex = 0;
        _frameLength = 0;
    }

    /// <summary>
    /// Parse frame data into specific frame types
    /// </summary>
    private ApiFrame? ParseFrame(byte[] data, int length)
    {
        if (length < 1)
            return null;

        var frame = new ApiFrame
        {
            FrameType = data[0],
            Data = new byte[length - 1]
        };
        Array.Copy(data, 1, frame.Data, 0, length - 1);

        switch (frame.FrameType)
        {
            case ApiFrameType.RxPacket:
                frame.RxPacket = ParseRxPacket(data, length);
                break;

            case ApiFrameType.ExplicitRxIndicator:
                frame.ExplicitRx = ParseExplicitRx(data, length);
                break;

            case ApiFrameType.TxStatus:
                frame.TxStatus = ParseTxStatus(data, length);
                break;

            case ApiFrameType.AtCommandResponse:
                frame.AtResponse = ParseAtResponse(data, length);
                break;
        }

        return frame;
    }

    /// <summary>
    /// Parse RX Packet (0x90) frame
    /// Format: [FrameType][64-bit Src][16-bit Src][Options][RF Data...]
    /// </summary>
    private RxPacketFrame? ParseRxPacket(byte[] data, int length)
    {
        // Minimum: 1 (type) + 8 (addr64) + 2 (addr16) + 1 (options) = 12
        if (length < 12)
        {
            OnParseError?.Invoke($"RX Packet too short: {length}");
            return null;
        }

        var frame = new RxPacketFrame
        {
            SourceAddress64 = ReadUInt64BE(data, 1),
            SourceAddress16 = ReadUInt16BE(data, 9),
            ReceiveOptions = data[11]
        };

        int rfDataLen = length - 12;
        if (rfDataLen > 0)
        {
            frame.RfData = new byte[rfDataLen];
            Array.Copy(data, 12, frame.RfData, 0, rfDataLen);
        }

        return frame;
    }

    /// <summary>
    /// Parse TX Status (0x8B) frame
    /// Format: [FrameType][FrameId][16-bit Dest][RetryCount][DeliveryStatus][DiscoveryStatus]
    /// </summary>
    private TxStatusFrame? ParseTxStatus(byte[] data, int length)
    {
        // Expect: 1 + 1 + 2 + 1 + 1 + 1 = 7
        if (length < 7)
        {
            OnParseError?.Invoke($"TX Status too short: {length}");
            return null;
        }

        return new TxStatusFrame
        {
            FrameId = data[1],
            DestAddress16 = ReadUInt16BE(data, 2),
            TransmitRetryCount = data[4],
            DeliveryStatus = (DeliveryStatus)data[5],
            DiscoveryStatus = data[6]
        };
    }

    private static ulong ReadUInt64BE(byte[] data, int offset)
    {
        return ((ulong)data[offset] << 56) |
               ((ulong)data[offset + 1] << 48) |
               ((ulong)data[offset + 2] << 40) |
               ((ulong)data[offset + 3] << 32) |
               ((ulong)data[offset + 4] << 24) |
               ((ulong)data[offset + 5] << 16) |
               ((ulong)data[offset + 6] << 8) |
               data[offset + 7];
    }

    /// <summary>
    /// Parse Explicit RX Indicator (0x91) frame
    /// Format: [FrameType][64-bit Src][16-bit Src][SrcEP][DestEP][ClusterID][ProfileID][Options][RF Data...]
    /// </summary>
    private ExplicitRxFrame? ParseExplicitRx(byte[] data, int length)
    {
        // Minimum: 1 (type) + 8 (addr64) + 2 (addr16) + 1 (srcEP) + 1 (destEP) + 2 (cluster) + 2 (profile) + 1 (options) = 18
        if (length < 18)
        {
            OnParseError?.Invoke($"Explicit RX too short: {length}");
            return null;
        }

        var frame = new ExplicitRxFrame
        {
            SourceAddress64 = ReadUInt64BE(data, 1),
            SourceAddress16 = ReadUInt16BE(data, 9),
            SourceEndpoint = data[11],
            DestEndpoint = data[12],
            ClusterId = ReadUInt16BE(data, 13),
            ProfileId = ReadUInt16BE(data, 15),
            ReceiveOptions = data[17]
        };

        int rfDataLen = length - 18;
        if (rfDataLen > 0)
        {
            frame.RfData = new byte[rfDataLen];
            Array.Copy(data, 18, frame.RfData, 0, rfDataLen);
        }

        return frame;
    }

    /// <summary>
    /// Parse AT Command Response (0x88) frame
    /// Format: [FrameType][FrameId][AT Command 2 chars][Status][Data...]
    /// </summary>
    private AtCommandResponseFrame? ParseAtResponse(byte[] data, int length)
    {
        // Minimum: 1 (type) + 1 (frameId) + 2 (command) + 1 (status) = 5
        if (length < 5)
        {
            OnParseError?.Invoke($"AT Response too short: {length}");
            return null;
        }

        var frame = new AtCommandResponseFrame
        {
            FrameId = data[1],
            Command = $"{(char)data[2]}{(char)data[3]}",
            Status = data[4]
        };

        int dataLen = length - 5;
        if (dataLen > 0)
        {
            frame.Data = new byte[dataLen];
            Array.Copy(data, 5, frame.Data, 0, dataLen);
        }

        return frame;
    }

    private static ushort ReadUInt16BE(byte[] data, int offset)
    {
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }
}
