using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using AnimatronicsControlCenter.Core.Utilities;

namespace AnimatronicsControlCenter.Infrastructure;

/// 기록 보기 화면이 거는 조건. 비어 있으면 그날 전부를 뜻한다.
public sealed record LogFilter(int? DeviceId, string? Result)
{
    public static readonly LogFilter None = new(null, null);
}

/// 기록 한 줄을 화면에 그대로 보여줄 형태로 담은 것.
public sealed record LogRecordRow(
    string Time,
    string Direction,
    string Device,
    string Command,
    string Result,
    string Length,
    string Elapsed,
    string Raw);

public sealed record LogReadResult(IReadOnlyList<LogRecordRow> Rows, bool IsTruncated);

/// 하루치 기록을 읽는다. 파일을 훑으면서 조건에 맞는 줄만 담으므로,
/// 메모리는 화면에 보일 양에 비례하지 파일 크기에 비례하지 않는다.
public static class LogFileReader
{
    public static LogReadResult Read(string directory, DateOnly date, LogFilter filter, int maxRows)
    {
        var rows = new List<LogRecordRow>();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return new LogReadResult(rows, false);
        }

        foreach (string path in FilesFor(directory, date))
        {
            // 한 줄씩 흘려 읽는다. 통째로 올리면 하루치 17만 줄이 그대로 메모리에 남는다.
            foreach (string line in ReadLinesShared(path))
            {
                if (line.Length == 0 || line[0] == '#') continue;

                string[] cells = SplitCsv(line);
                if (cells.Length < 8) continue;
                if (cells[0] == "시각") continue;
                if (!Matches(cells, filter)) continue;

                if (rows.Count >= maxRows)
                {
                    return new LogReadResult(rows, true);
                }

                rows.Add(new LogRecordRow(
                    cells[0], cells[1], cells[2], cells[3], cells[4], cells[5], cells[6], cells[7]));
            }
        }

        return new LogReadResult(rows, false);
    }

    /// 기록 파일은 앱이 도는 내내 기록기가 열어 둔 채 쓰고 있다.
    /// 쓰기 공유를 허용해서 열지 않으면 "다른 프로세스가 사용 중" 으로 읽기가 실패한다.
    public static IEnumerable<string> ReadLinesShared(string path)
    {
        using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            yield return line;
        }
    }

    /// 같은 날짜의 파일을 번호 순으로 돌려준다. 하루가 여러 개로 쪼개져 있어도 시각 순서가 유지된다.
    private static IEnumerable<string> FilesFor(string directory, DateOnly date)
    {
        for (int sequence = 0; ; sequence++)
        {
            string path = Path.Combine(directory, LogFilePolicy.FileName(date, sequence));
            if (!File.Exists(path)) yield break;

            yield return path;
        }
    }

    private static bool Matches(string[] cells, LogFilter filter)
    {
        if (filter.DeviceId is int device &&
            !string.Equals(cells[2], device.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filter.Result) &&
            !string.Equals(cells[4], filter.Result, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string[] SplitCsv(string line)
    {
        var cells = new List<string>(8);
        var cell = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c != '"') { cell.Append(c); continue; }

                // 따옴표 두 개는 값 안의 따옴표 하나를 뜻한다.
                if (i + 1 < line.Length && line[i + 1] == '"') { cell.Append('"'); i++; continue; }

                quoted = false;
                continue;
            }

            if (c == '"') { quoted = true; continue; }
            if (c == ',') { cells.Add(cell.ToString()); cell.Clear(); continue; }

            cell.Append(c);
        }

        cells.Add(cell.ToString());
        return cells.ToArray();
    }
}
