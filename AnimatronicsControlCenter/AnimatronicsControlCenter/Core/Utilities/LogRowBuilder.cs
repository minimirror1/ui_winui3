using System;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Protocol;

namespace AnimatronicsControlCenter.Core.Utilities;

/// 기록기가 넘겨준 송수신 한 건을 기록 파일의 한 줄로 바꾼다.
/// 해독은 기록 스레드에서만 일어난다. 통신 경로에서는 절대 부르지 않는다.
public static class LogRowBuilder
{
    public static LogRow FromEntry(SerialTrafficEntry entry, bool includeRaw)
    {
        string raw = includeRaw ? entry.Line ?? string.Empty : string.Empty;
        BinaryPacketDecodeResult decoded = BinaryPacketDecoder.DecodeHex(entry.Line ?? string.Empty);

        if (!decoded.IsValid)
        {
            return new LogRow(entry.Timestamp, entry.Direction, null, string.Empty, string.Empty, null, null, raw);
        }

        // 호스트는 항상 0번이다. 응답이면 보낸 쪽이, 요청이면 받는 쪽이 장치다.
        int deviceId = decoded.IsResponse ? decoded.SrcId : decoded.TarId;

        return new LogRow(
            entry.Timestamp,
            entry.Direction,
            deviceId,
            decoded.Command,
            decoded.Status ?? string.Empty,
            decoded.PayloadLength,
            null,
            raw);
    }
}
