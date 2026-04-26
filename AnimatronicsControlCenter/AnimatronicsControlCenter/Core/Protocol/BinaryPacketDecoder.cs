using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnimatronicsControlCenter.Core.Protocol;

public static class BinaryPacketDecoder
{
    public static BinaryPacketDecodeResult Decode(ReadOnlySpan<byte> data)
    {
        string rawHex = ToHex(data);
        if (data.IsEmpty)
        {
            return Invalid(false, "", null, 0, 0, 0, "empty packet", rawHex);
        }

        if (TryDecodeResponse(data, rawHex, out var response))
        {
            return response;
        }

        if (TryDecodeRequest(data, rawHex, out var request))
        {
            return request;
        }

        return Invalid(false, "", null, 0, 0, 0, "Cannot parse binary header", rawHex);
    }

    public static BinaryPacketDecodeResult DecodeHex(string hex)
    {
        if (!TryParseHex(hex, out var data))
        {
            return new BinaryPacketDecodeResult(
                IsValid: false,
                IsResponse: false,
                Command: "",
                Status: null,
                SrcId: 0,
                TarId: 0,
                PayloadLength: 0,
                Summary: "Invalid hex packet",
                Details: hex,
                RawHex: hex,
                ParseError: "Not a binary hex packet");
        }

        return Decode(data);
    }

    private static bool TryDecodeResponse(ReadOnlySpan<byte> data, string rawHex, out BinaryPacketDecodeResult result)
    {
        result = default!;
        if (data.Length < BinaryProtocolConst.ResponseHeaderSize) return false;
        if (!BinaryDeserializer.TryParseResponseHeader(data, out var header)) return false;
        if (!Enum.IsDefined(header.Status)) return false;

        int actualPayloadLength = data.Length - BinaryProtocolConst.ResponseHeaderSize;
        if (actualPayloadLength != header.PayloadLen)
        {
            string error = $"payload length mismatch: header={header.PayloadLen} actual={actualPayloadLength}";
            result = Invalid(
                isResponse: true,
                command: header.Cmd.ToString(),
                status: header.Status.ToString(),
                srcId: header.SrcId,
                tarId: header.TarId,
                payloadLength: header.PayloadLen,
                parseError: error,
                rawHex: rawHex);
            return true;
        }

        ReadOnlySpan<byte> payload = data.Slice(BinaryProtocolConst.ResponseHeaderSize, header.PayloadLen);
        string command = header.Cmd.ToString();
        string status = header.Status.ToString();
        string details = DecodeResponsePayload(header, payload);
        string summary = $"{command} {status.ToUpperInvariant()}";
        string protocolName = ProtocolCommandName(header.Cmd);
        if (!string.Equals(protocolName, command, StringComparison.Ordinal))
        {
            summary += $" ({protocolName} {status.ToUpperInvariant()})";
        }

        result = new BinaryPacketDecodeResult(
            IsValid: true,
            IsResponse: true,
            Command: command,
            Status: status,
            SrcId: header.SrcId,
            TarId: header.TarId,
            PayloadLength: header.PayloadLen,
            Summary: summary,
            Details: details,
            RawHex: rawHex,
            ParseError: null);
        return true;
    }

    private static bool TryDecodeRequest(ReadOnlySpan<byte> data, string rawHex, out BinaryPacketDecodeResult result)
    {
        result = default!;
        if (data.Length < BinaryProtocolConst.RequestHeaderSize) return false;
        if (!BinaryDeserializer.TryParseRequestHeader(data, out var header)) return false;

        int actualPayloadLength = data.Length - BinaryProtocolConst.RequestHeaderSize;
        if (actualPayloadLength != header.PayloadLen)
        {
            string error = $"payload length mismatch: header={header.PayloadLen} actual={actualPayloadLength}";
            result = Invalid(
                isResponse: false,
                command: header.Cmd.ToString(),
                status: null,
                srcId: header.SrcId,
                tarId: header.TarId,
                payloadLength: header.PayloadLen,
                parseError: error,
                rawHex: rawHex);
            return true;
        }

        ReadOnlySpan<byte> payload = data.Slice(BinaryProtocolConst.RequestHeaderSize, header.PayloadLen);
        result = new BinaryPacketDecodeResult(
            IsValid: true,
            IsResponse: false,
            Command: header.Cmd.ToString(),
            Status: null,
            SrcId: header.SrcId,
            TarId: header.TarId,
            PayloadLength: header.PayloadLen,
            Summary: $"{header.Cmd} REQUEST ({ProtocolCommandName(header.Cmd)} REQUEST)",
            Details: DecodeRequestPayload(header, payload),
            RawHex: rawHex,
            ParseError: null);
        return true;
    }

    private static BinaryPacketDecodeResult Invalid(
        bool isResponse,
        string command,
        string? status,
        byte srcId,
        byte tarId,
        ushort payloadLength,
        string parseError,
        string rawHex)
    {
        string summary = string.IsNullOrWhiteSpace(command)
            ? parseError
            : $"{command} parse error";

        return new BinaryPacketDecodeResult(
            IsValid: false,
            IsResponse: isResponse,
            Command: command,
            Status: status,
            SrcId: srcId,
            TarId: tarId,
            PayloadLength: payloadLength,
            Summary: summary,
            Details: $"error={parseError}{Environment.NewLine}raw={rawHex}",
            RawHex: rawHex,
            ParseError: parseError);
    }

    private static string DecodeResponsePayload(ResponseHeader header, ReadOnlySpan<byte> payload)
    {
        if (header.Status == ResponseStatus.Error || header.Cmd == BinaryCommand.Error)
        {
            var (code, message) = BinaryDeserializer.ParseErrorResponse(payload);
            string readable = BinaryProtocolErrorText.Describe(code, header.Cmd);
            return Lines(
                $"type=RESPONSE",
                $"cmd={header.Cmd}",
                $"status={header.Status}",
                $"src={header.SrcId}",
                $"tar={header.TarId}",
                $"payload_len={header.PayloadLen}",
                $"error_code={code}",
                $"message={message}",
                $"description={readable}",
                $"raw={ToHex(payload)}");
        }

        return header.Cmd switch
        {
            BinaryCommand.Pong => DecodePong(payload, header),
            BinaryCommand.SaveFile => DecodePathPayload(payload, header, "path"),
            BinaryCommand.VerifyFile => DecodeVerify(payload, header),
            BinaryCommand.MotionCtrl => DecodeMotionCtrl(payload, header),
            BinaryCommand.Move => DecodeMove(payload, header),
            BinaryCommand.GetMotors => DecodeMotorList(payload, header, fullSnapshot: true),
            BinaryCommand.GetMotorState => DecodeMotorList(payload, header, fullSnapshot: false),
            BinaryCommand.GetFiles => DecodeFiles(payload, header),
            BinaryCommand.GetFile => DecodeFile(payload, header),
            _ => HeaderLines(header, payload),
        };
    }

    private static string DecodeRequestPayload(RequestHeader header, ReadOnlySpan<byte> payload)
    {
        if (header.Cmd == BinaryCommand.Ping)
        {
            return DecodePingRequest(payload, header);
        }

        return Lines(
            $"type=REQUEST",
            $"cmd={header.Cmd}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"raw={ToHex(payload)}");
    }

    private static string DecodePingRequest(ReadOnlySpan<byte> payload, RequestHeader header)
    {
        if (payload.Length == 0)
        {
            return Lines(
                "type=REQUEST",
                $"cmd={header.Cmd}",
                $"src={header.SrcId}",
                $"tar={header.TarId}",
                $"payload_len={header.PayloadLen}",
                "time_payload=absent",
                $"raw={ToHex(payload)}");
        }

        if (payload.Length != BinaryProtocolConst.PingTimePayloadSize)
        {
            return Lines(
                "type=REQUEST",
                $"cmd={header.Cmd}",
                $"src={header.SrcId}",
                $"tar={header.TarId}",
                $"payload_len={header.PayloadLen}",
                "payload_error=invalid PING time payload length",
                $"raw={ToHex(payload)}");
        }

        byte timeFormat = payload[0];
        string countryCode = Encoding.ASCII.GetString(payload.Slice(1, 2));
        ushort year = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(3, 2));
        byte month = payload[5];
        byte day = payload[6];
        byte hour = payload[7];
        byte minute = payload[8];
        byte second = payload[9];
        short utcOffsetMin = BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(10, 2));

        return Lines(
            "type=REQUEST",
            $"cmd={header.Cmd}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"time_fmt={timeFormat}",
            $"country_code={countryCode}",
            $"timestamp={year:D4}-{month:D2}-{day:D2} {hour:D2}:{minute:D2}:{second:D2}",
            $"utc_offset_min={utcOffsetMin}",
            $"raw={ToHex(payload)}");
    }

    private static string DecodePong(ReadOnlySpan<byte> payload, ResponseHeader header)
    {
        if (!BinaryDeserializer.TryParsePongResponse(payload, out var status))
        {
            return MalformedPayload(header, payload, "invalid PONG payload");
        }

        return Lines(
            $"type=RESPONSE",
            $"cmd={header.Cmd}",
            $"status={header.Status}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"state={status.State}",
            $"init_state={status.InitState}",
            $"current_ms={status.CurrentMs}",
            $"total_ms={status.TotalMs}",
            $"current={TimeSpan.FromMilliseconds(status.CurrentMs):c}",
            $"total={TimeSpan.FromMilliseconds(status.TotalMs):c}",
            $"raw={ToHex(payload)}");
    }

    private static string DecodeVerify(ReadOnlySpan<byte> payload, ResponseHeader header)
    {
        if (!TryReadPath(payload, out string path, out int offset))
        {
            return MalformedPayload(header, payload, "invalid VERIFY_FILE payload");
        }

        if (payload.Length != offset + 1)
        {
            return MalformedPayload(header, payload, "invalid VERIFY_FILE match flag");
        }

        bool match = payload[offset] != 0;
        return Lines(
            $"type=RESPONSE",
            $"cmd={header.Cmd}",
            $"status={header.Status}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"path={path}",
            $"match={match}",
            $"raw={ToHex(payload)}");
    }

    private static string DecodePathPayload(ReadOnlySpan<byte> payload, ResponseHeader header, string fieldName)
    {
        if (!TryReadPath(payload, out string path, out int offset) || offset != payload.Length)
        {
            return MalformedPayload(header, payload, $"invalid {header.Cmd} payload");
        }

        return Lines(
            $"type=RESPONSE",
            $"cmd={header.Cmd}",
            $"status={header.Status}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"{fieldName}={path}",
            $"raw={ToHex(payload)}");
    }

    private static string DecodeMotionCtrl(ReadOnlySpan<byte> payload, ResponseHeader header)
    {
        var (action, deviceId) = BinaryDeserializer.ParseMotionCtrlResponse(payload);
        return Lines(
            $"type=RESPONSE",
            $"cmd={header.Cmd}",
            $"status={header.Status}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"action={action}",
            $"device_id={deviceId}",
            $"raw={ToHex(payload)}");
    }

    private static string DecodeMove(ReadOnlySpan<byte> payload, ResponseHeader header)
    {
        var (deviceId, motorId) = BinaryDeserializer.ParseMoveResponse(payload);
        return Lines(
            $"type=RESPONSE",
            $"cmd={header.Cmd}",
            $"status={header.Status}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"device_id={deviceId}",
            $"motor_id={motorId}",
            $"raw={ToHex(payload)}");
    }

    private static string DecodeMotorList(ReadOnlySpan<byte> payload, ResponseHeader header, bool fullSnapshot)
    {
        var motors = fullSnapshot
            ? BinaryDeserializer.ParseGetMotorsResponse(payload)
            : BinaryDeserializer.ParseMotorStateResponse(payload);
        var lines = new List<string>
        {
            "type=RESPONSE",
            $"cmd={header.Cmd}",
            $"status={header.Status}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"motor_count={motors.Count}",
        };

        foreach (var motor in motors.Take(20))
        {
            lines.Add($"motor id={motor.Id} status={motor.Status} pos={motor.Position}");
        }

        if (motors.Count > 20)
        {
            lines.Add($"motor_preview_truncated={motors.Count - 20}");
        }

        lines.Add($"raw={ToHex(payload)}");
        return Lines(lines);
    }

    private static string DecodeFiles(ReadOnlySpan<byte> payload, ResponseHeader header)
    {
        var files = BinaryDeserializer.ParseGetFilesResponse(payload);
        var lines = new List<string>
        {
            "type=RESPONSE",
            $"cmd={header.Cmd}",
            $"status={header.Status}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"entry_count={files.Count}",
        };

        foreach (var file in files.Take(20))
        {
            lines.Add($"{(file.IsDirectory ? "dir" : "file")} path={file.Path} size={file.Size}");
        }

        if (files.Count > 20)
        {
            lines.Add($"entry_preview_truncated={files.Count - 20}");
        }

        lines.Add($"raw={ToHex(payload)}");
        return Lines(lines);
    }

    private static string DecodeFile(ReadOnlySpan<byte> payload, ResponseHeader header)
    {
        var (path, content) = BinaryDeserializer.ParseGetFileResponse(payload);
        string preview = content.Length <= 80 ? content : content[..80] + "...";
        return Lines(
            $"type=RESPONSE",
            $"cmd={header.Cmd}",
            $"status={header.Status}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"path={path}",
            $"content_len={Encoding.UTF8.GetByteCount(content)}",
            $"preview={preview}",
            $"raw={ToHex(payload)}");
    }

    private static string HeaderLines(ResponseHeader header, ReadOnlySpan<byte> payload)
        => Lines(
            $"type=RESPONSE",
            $"cmd={header.Cmd}",
            $"status={header.Status}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"raw={ToHex(payload)}");

    private static string MalformedPayload(ResponseHeader header, ReadOnlySpan<byte> payload, string error)
        => Lines(
            $"type=RESPONSE",
            $"cmd={header.Cmd}",
            $"status={header.Status}",
            $"src={header.SrcId}",
            $"tar={header.TarId}",
            $"payload_len={header.PayloadLen}",
            $"payload_error={error}",
            $"raw={ToHex(payload)}");

    private static bool TryReadPath(ReadOnlySpan<byte> payload, out string path, out int offset)
    {
        path = string.Empty;
        offset = 0;
        if (payload.Length < 2) return false;

        ushort pathLen = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (payload.Length < 2 + pathLen) return false;

        path = Encoding.UTF8.GetString(payload.Slice(2, pathLen));
        offset = 2 + pathLen;
        return true;
    }

    private static bool TryParseHex(string hex, out byte[] data)
    {
        data = Array.Empty<byte>();
        try
        {
            string[] tokens = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            data = new byte[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                data[i] = Convert.ToByte(tokens[i], 16);
            }

            return true;
        }
        catch
        {
            data = Array.Empty<byte>();
            return false;
        }
    }

    private static string ProtocolCommandName(BinaryCommand command) => command switch
    {
        BinaryCommand.GetMotors => "GET_MOTORS",
        BinaryCommand.GetMotorState => "GET_MOTOR_STATE",
        BinaryCommand.GetFiles => "GET_FILES",
        BinaryCommand.GetFile => "GET_FILE",
        BinaryCommand.SaveFile => "SAVE_FILE",
        BinaryCommand.VerifyFile => "VERIFY_FILE",
        BinaryCommand.MotionCtrl => "MOTION_CTRL",
        _ => command.ToString().ToUpperInvariant(),
    };

    private static string ToHex(ReadOnlySpan<byte> bytes)
        => string.Join(" ", bytes.ToArray().Select(b => b.ToString("X2")));

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

    private static string Lines(IEnumerable<string> lines) => string.Join(Environment.NewLine, lines);
}
