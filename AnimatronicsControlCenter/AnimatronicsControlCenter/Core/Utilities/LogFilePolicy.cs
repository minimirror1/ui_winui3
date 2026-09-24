using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AnimatronicsControlCenter.Core.Utilities;

/// 정리 판단에 쓰는 기록 파일 하나의 이름과 크기.
public readonly record struct LogFileEntry(string FileName, long SizeBytes);

/// 통신 기록 파일의 이름·보관·정리 규칙. 파일 시스템을 건드리지 않는 순수 판단만 담는다.
public static class LogFilePolicy
{
    public static string FileName(DateOnly date, int sequence)
        => sequence <= 0
            ? $"{date:yyyy-MM-dd}.csv"
            : $"{date:yyyy-MM-dd}.{sequence}.csv";

    /// "2026-09-24.csv" 와 "2026-09-24.7.csv" 는 같은 날짜로 읽힌다.
    public static bool TryParseDate(string fileName, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrEmpty(fileName)) return false;
        if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)) return false;
        if (fileName.Length < DatePrefixLength) return false;

        return DateOnly.TryParseExact(
            fileName[..DatePrefixLength],
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    /// 보관 기간보다 더 오래된 기록 파일만 고른다. 경계 날짜(정확히 retentionDays 전)는 남긴다.
    public static IReadOnlyList<string> SelectExpired(
        IEnumerable<string> fileNames, DateOnly today, int retentionDays)
    {
        var expired = new List<string>();
        if (fileNames == null) return expired;

        DateOnly oldestKept = today.AddDays(-retentionDays);
        foreach (string name in fileNames)
        {
            if (TryParseDate(name, out DateOnly date) && date < oldestKept)
            {
                expired.Add(name);
            }
        }

        return expired;
    }

    /// 전체 크기가 상한을 넘으면 오래된 파일부터 고른다. 지금 쓰고 있을 최신 파일은 절대 고르지 않는다.
    public static IReadOnlyList<string> SelectOverCap(
        IEnumerable<LogFileEntry> files, long capBytes)
    {
        var doomed = new List<string>();
        if (files == null) return doomed;

        var ordered = new List<LogFileEntry>(files);
        ordered.Sort(static (a, b) => OrderKey(a.FileName).CompareTo(OrderKey(b.FileName)));

        long total = 0;
        foreach (LogFileEntry file in ordered) total += file.SizeBytes;

        // 마지막 하나는 남겨 둔다: 기록 중인 파일을 지우면 지금 남기는 내용을 잃는다.
        for (int i = 0; i < ordered.Count - 1 && total > capBytes; i++)
        {
            doomed.Add(ordered[i].FileName);
            total -= ordered[i].SizeBytes;
        }

        return doomed;
    }

    /// 같은 날이면 번호 없는 파일이 먼저다. 번호는 그 뒤에 1, 2, … 순으로 붙는다.
    private static (DateOnly Date, int Sequence) OrderKey(string fileName)
    {
        DateOnly date = TryParseDate(fileName, out DateOnly parsed) ? parsed : DateOnly.MinValue;
        string middle = fileName.Length > DatePrefixLength
            ? fileName[DatePrefixLength..].Trim('.')
            : string.Empty;

        int dot = middle.IndexOf('.');
        if (dot > 0 && int.TryParse(middle[..dot], NumberStyles.None, CultureInfo.InvariantCulture, out int sequence))
        {
            return (date, sequence);
        }

        return (date, 0);
    }

    /// 기록은 설정 파일이 있는 폴더 옆 Logs 에 모은다. 설정 파일을 찾는 경로 규칙을 그대로 따른다.
    public static string FolderFor(string settingsFilePath)
    {
        string? directory = Path.GetDirectoryName(settingsFilePath);
        return string.IsNullOrWhiteSpace(directory)
            ? FolderName
            : Path.Combine(directory, FolderName);
    }

    public const string FolderName = "Logs";
    public const int DefaultRetentionDays = 100;
    public const long DefaultMaxFileBytes = 50L * 1024 * 1024;
    public const long DefaultMaxTotalBytes = 3L * 1024 * 1024 * 1024;

    private const int DatePrefixLength = 10;
}
