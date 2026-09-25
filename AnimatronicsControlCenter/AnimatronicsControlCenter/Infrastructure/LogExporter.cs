using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AnimatronicsControlCenter.Core.Utilities;

namespace AnimatronicsControlCenter.Infrastructure;

/// 보관 중인 기록을 파일 하나로 합쳐 내보낸다. 누군가에게 통째로 넘길 때 쓴다.
public static class LogExporter
{
    public static void Export(string directory, string targetPath)
    {
        using var writer = new StreamWriter(
            new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        writer.WriteLine(LogCsvLine.Header);

        foreach (string path in OrderedLogFiles(directory))
        {
            // 기록기가 열어 둔 파일이라 공유 읽기로 연다.
            foreach (string line in LogFileReader.ReadLinesShared(path))
            {
                // 머리글은 합친 파일 맨 위에 한 번만 둔다. 구멍 표시는 그대로 살린다.
                if (line.Length == 0 || line == LogCsvLine.Header) continue;

                writer.WriteLine(line);
            }
        }
    }

    private static List<string> OrderedLogFiles(string directory)
    {
        var files = new List<(DateOnly Date, string Name, string Path)>();
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return new List<string>();

        foreach (string path in Directory.EnumerateFiles(directory))
        {
            string name = Path.GetFileName(path);
            if (LogFilePolicy.TryParseDate(name, out DateOnly date))
            {
                files.Add((date, name, path));
            }
        }

        files.Sort(static (a, b) =>
        {
            int byDate = a.Date.CompareTo(b.Date);
            return byDate != 0 ? byDate : string.CompareOrdinal(a.Name, b.Name);
        });

        var ordered = new List<string>(files.Count);
        foreach (var file in files) ordered.Add(file.Path);
        return ordered;
    }
}
