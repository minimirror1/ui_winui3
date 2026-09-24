using System;
using System.Globalization;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Utilities;

/// 기록 파일에 적히는 한 줄. 값이 없는 칸은 빈 칸으로 남는다.
public readonly record struct LogRow(
    DateTimeOffset Timestamp,
    SerialTrafficDirection Direction,
    int? DeviceId,
    string Command,
    string Result,
    int? PayloadLength,
    long? ElapsedMs,
    string Raw);

/// LogRow 를 CSV 한 줄로 바꾼다. 파일 시스템을 건드리지 않는 순수 변환만 담는다.
public static class LogCsvLine
{
    public const string Header = "시각,방향,장치,명령,결과,길이,소요(ms),원문";

    public static string Format(LogRow row)
    {
        // 시각은 값이 지닌 시간대로 찍는다. 기계의 현재 시간대에 따라 달라지면 기록이 흔들린다.
        string time = row.Timestamp.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        string direction = row.Direction == SerialTrafficDirection.Tx ? "TX" : "RX";

        return string.Join(',',
            time,
            direction,
            Number(row.DeviceId),
            Escape(row.Command),
            Escape(row.Result),
            Number(row.PayloadLength),
            Number(row.ElapsedMs),
            Escape(row.Raw));
    }

    private static string Number(long? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.IndexOfAny(MustQuote) < 0) return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static readonly char[] MustQuote = { ',', '"', '\r', '\n' };
}
