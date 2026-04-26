using System;
using System.Buffers.Binary;
using System.Text;

namespace AnimatronicsControlCenter.Core.Protocol;

/// <summary>PC → STM32 방향 요청 인코딩. 모든 메서드는 완전한 패킷(헤더+페이로드)을 반환.</summary>
public static class BinarySerializer
{
    // ── 공통 헤더 빌더 ──────────────────────────────────────────────

    private static byte[] BuildRequest(byte srcId, byte tarId, BinaryCommand cmd, ReadOnlySpan<byte> payload)
    {
        var buf = new byte[BinaryProtocolConst.RequestHeaderSize + payload.Length];
        buf[0] = srcId;
        buf[1] = tarId;
        buf[2] = (byte)cmd;
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(3), (ushort)payload.Length);
        payload.CopyTo(buf.AsSpan(BinaryProtocolConst.RequestHeaderSize));
        return buf;
    }

    // ── 명령별 인코딩 ────────────────────────────────────────────────

    /// §4.1 PING — payload 없음
    public static byte[] EncodePing(byte srcId, byte tarId)
        => BuildRequest(srcId, tarId, BinaryCommand.Ping, ReadOnlySpan<byte>.Empty);

    public static byte[] EncodePing(byte srcId, byte tarId, PingTimePayload timePayload)
    {
        var countryCode = NormalizeCountryCode(timePayload.CountryCode);
        var timestamp = timePayload.Timestamp;
        var payload = new byte[BinaryProtocolConst.PingTimePayloadSize];

        payload[0] = BinaryProtocolConst.PingTimeFormatLocal;
        payload[1] = (byte)countryCode[0];
        payload[2] = (byte)countryCode[1];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(3), checked((ushort)timestamp.Year));
        payload[5] = checked((byte)timestamp.Month);
        payload[6] = checked((byte)timestamp.Day);
        payload[7] = checked((byte)timestamp.Hour);
        payload[8] = checked((byte)timestamp.Minute);
        payload[9] = checked((byte)timestamp.Second);
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(10), checked((short)timestamp.Offset.TotalMinutes));

        return BuildRequest(srcId, tarId, BinaryCommand.Ping, payload);
    }

    /// §4.2 MOVE — motor_id(1) + pos(2)
    public static byte[] EncodeMove(byte srcId, byte tarId, byte motorId, double posRaw)
    {
        var payload = new byte[3];
        payload[0] = motorId;
        var pos = (ushort)Math.Clamp(Math.Round(posRaw), 0, 65535);
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(1), pos);
        return BuildRequest(srcId, tarId, BinaryCommand.Move, payload);
    }

    /// §4.3 MOTION_CTRL — action(1) [+ time_ms(4) if SEEK]
    public static byte[] EncodeMotionCtrl(byte srcId, byte tarId, BinaryMotionAction action, double? timeSeconds = null)
    {
        byte[] payload;
        if (action == BinaryMotionAction.Seek && timeSeconds.HasValue)
        {
            payload = new byte[5];
            payload[0] = (byte)action;
            var timeMs = (uint)Math.Clamp(Math.Round(timeSeconds.Value * 1000), 0, (double)uint.MaxValue);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(1), timeMs);
        }
        else
        {
            payload = [(byte)action];
        }
        return BuildRequest(srcId, tarId, BinaryCommand.MotionCtrl, payload);
    }

    /// §4.4 GET_MOTORS — payload 없음
    public static byte[] EncodeGetMotors(byte srcId, byte tarId)
        => BuildRequest(srcId, tarId, BinaryCommand.GetMotors, ReadOnlySpan<byte>.Empty);

    /// §4.5 GET_MOTOR_STATE — payload 없음
    public static byte[] EncodeGetMotorState(byte srcId, byte tarId)
        => BuildRequest(srcId, tarId, BinaryCommand.GetMotorState, ReadOnlySpan<byte>.Empty);

    /// §4.6 GET_FILES — payload 없음
    public static byte[] EncodeGetFiles(byte srcId, byte tarId)
        => BuildRequest(srcId, tarId, BinaryCommand.GetFiles, ReadOnlySpan<byte>.Empty);

    /// §4.7 GET_FILE — path_len(2) + path
    public static byte[] EncodeGetFile(byte srcId, byte tarId, string path)
    {
        var pathBytes = Encoding.UTF8.GetBytes(path);
        var payload   = new byte[2 + pathBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(0), (ushort)pathBytes.Length);
        pathBytes.CopyTo(payload.AsSpan(2));
        return BuildRequest(srcId, tarId, BinaryCommand.GetFile, payload);
    }

    /// §4.8 SAVE_FILE — path_len(2)+path + content_len(2)+content
    public static byte[] EncodeSaveFile(byte srcId, byte tarId, string path, string content)
        => EncodePathContent(srcId, tarId, BinaryCommand.SaveFile, path, content);

    /// §4.9 VERIFY_FILE — SAVE_FILE 요청과 동일 구조
    public static byte[] EncodeVerifyFile(byte srcId, byte tarId, string path, string content)
        => EncodePathContent(srcId, tarId, BinaryCommand.VerifyFile, path, content);

    private static byte[] EncodePathContent(byte srcId, byte tarId, BinaryCommand cmd, string path, string content)
    {
        var pathBytes    = Encoding.UTF8.GetBytes(path);
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var payload      = new byte[2 + pathBytes.Length + 2 + contentBytes.Length];
        int offset       = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset), (ushort)pathBytes.Length);
        offset += 2;
        pathBytes.CopyTo(payload.AsSpan(offset));
        offset += pathBytes.Length;
        BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(offset), (ushort)contentBytes.Length);
        offset += 2;
        contentBytes.CopyTo(payload.AsSpan(offset));
        return BuildRequest(srcId, tarId, cmd, payload);
    }

    private static string NormalizeCountryCode(string countryCode)
    {
        if (countryCode.Length != 2 ||
            !char.IsAsciiLetter(countryCode[0]) ||
            !char.IsAsciiLetter(countryCode[1]))
        {
            throw new ArgumentException("Country code must be two ASCII letters.", nameof(countryCode));
        }

        return countryCode.ToUpperInvariant();
    }
}
