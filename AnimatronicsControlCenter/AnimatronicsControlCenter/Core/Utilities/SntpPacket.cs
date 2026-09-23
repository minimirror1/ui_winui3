using System;
using System.Buffers.Binary;

namespace AnimatronicsControlCenter.Core.Utilities;

/// RFC 4330 SNTP 48바이트 패킷 생성·파싱 유틸리티.
public static class SntpPacket
{
    public const int PacketSize = 48;
    private const int ReceiveTimestampOffset = 32;  // T2
    private const int TransmitTimestampOffset = 40; // T3

    private static readonly DateTimeOffset NtpEpoch = new(1900, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] BuildRequest()
    {
        var packet = new byte[PacketSize];
        packet[0] = 0x23; // LI=0, VN=4, Mode=3 (client)
        return packet;
    }

    public static void WriteTimestamp(byte[] buffer, int offset, DateTimeOffset time)
    {
        double totalSeconds = (time.ToUniversalTime() - NtpEpoch).TotalSeconds;
        uint seconds = (uint)totalSeconds; // 2036년 era 래핑 전까지 유효
        uint fraction = (uint)((totalSeconds - Math.Floor(totalSeconds)) * 4294967296.0);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset), seconds);
        BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset + 4), fraction);
    }

    public static DateTimeOffset ReadTimestamp(ReadOnlySpan<byte> buffer, int offset)
    {
        uint seconds = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(offset, 4));
        uint fraction = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(offset + 4, 4));
        return NtpEpoch.AddSeconds(seconds + fraction / 4294967296.0);
    }

    /// 서버 응답에서 시계 보정값을 계산한다.
    /// correction = ((T2−T1)+(T3−T4))/2 — 왕복 지연이 대칭이라 가정하는 표준 NTP 오프셋 공식.
    public static bool TryParseCorrection(
        ReadOnlySpan<byte> response,
        DateTimeOffset requestSentUtc,
        DateTimeOffset responseReceivedUtc,
        out TimeSpan correction,
        out string? error)
    {
        correction = TimeSpan.Zero;

        if (response.Length < PacketSize)
        {
            error = $"Response too short: {response.Length} bytes.";
            return false;
        }

        int mode = response[0] & 0x07;
        if (mode != 4)
        {
            error = $"Not a server response (mode={mode}).";
            return false;
        }

        int stratum = response[1];
        if (stratum is < 1 or > 15)
        {
            error = $"Invalid stratum {stratum} (kiss-of-death or unsynchronized).";
            return false;
        }

        if (BinaryPrimitives.ReadUInt64BigEndian(response.Slice(TransmitTimestampOffset, 8)) == 0)
        {
            error = "Transmit timestamp is zero.";
            return false;
        }

        var t2 = ReadTimestamp(response, ReceiveTimestampOffset);
        var t3 = ReadTimestamp(response, TransmitTimestampOffset);
        correction = ((t2 - requestSentUtc.ToUniversalTime()) + (t3 - responseReceivedUtc.ToUniversalTime())) / 2.0;
        error = null;
        return true;
    }
}
