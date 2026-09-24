using System;
using System.Collections.Generic;
using System.IO;
using AnimatronicsControlCenter.Core.Utilities;

namespace AnimatronicsControlCenter.Infrastructure;

/// 오래됐거나 용량 상한을 넘긴 기록 파일을 지운다. 앱을 켤 때 한 번 돈다.
public static class LogFileCleaner
{
    public static int Clean(string directory, DateOnly today, int retentionDays, long maxTotalBytes)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return 0;

        var logs = new List<LogFileEntry>();
        foreach (string path in Directory.EnumerateFiles(directory))
        {
            string name = Path.GetFileName(path);
            if (!LogFilePolicy.TryParseDate(name, out _)) continue;

            logs.Add(new LogFileEntry(name, new FileInfo(path).Length));
        }

        var doomed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>(logs.Count);
        foreach (LogFileEntry log in logs) names.Add(log.FileName);

        foreach (string name in LogFilePolicy.SelectExpired(names, today, retentionDays))
        {
            doomed.Add(name);
        }

        // 기간 정리로 줄어든 뒤의 크기로 용량을 다시 따진다.
        var survivors = new List<LogFileEntry>();
        foreach (LogFileEntry log in logs)
        {
            if (!doomed.Contains(log.FileName)) survivors.Add(log);
        }

        foreach (string name in LogFilePolicy.SelectOverCap(survivors, maxTotalBytes))
        {
            doomed.Add(name);
        }

        int removed = 0;
        foreach (string name in doomed)
        {
            try
            {
                File.Delete(Path.Combine(directory, name));
                removed++;
            }
            catch
            {
                // 지우지 못해도 앱은 계속 돌아야 한다. 다음 시작에 다시 시도한다.
            }
        }

        return removed;
    }
}
