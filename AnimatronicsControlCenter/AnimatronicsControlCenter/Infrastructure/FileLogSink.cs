using System;
using System.IO;
using System.Text;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Utilities;

namespace AnimatronicsControlCenter.Infrastructure;

/// 기록을 날짜별 CSV 파일로 남긴다. 날짜가 바뀌거나 파일이 상한에 닿으면 다음 파일로 넘긴다.
public sealed class FileLogSink : ILogSink
{
    private readonly string _directory;
    private readonly long _maxFileBytes;

    private StreamWriter? _stream;
    private DateOnly _openDate;
    private int _openSequence;
    private long _openBytes;

    public FileLogSink(string directory, long maxFileBytes)
    {
        _directory = directory;
        _maxFileBytes = maxFileBytes;
        Directory.CreateDirectory(_directory);
    }

    public void Write(DateOnly date, string line)
    {
        EnsureFile(date);
        if (_stream == null) return;

        _stream.WriteLine(line);
        _openBytes += Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length;
    }

    public void Flush()
    {
        try { _stream?.Flush(); } catch { }
    }

    public void Dispose() => CloseFile();

    private void EnsureFile(DateOnly date)
    {
        bool dayChanged = _stream != null && date != _openDate;
        bool tooBig = _stream != null && _openBytes >= _maxFileBytes;

        if (_stream != null && !dayChanged && !tooBig) return;

        int sequence = dayChanged || _stream == null ? StartingSequence(date) : _openSequence + 1;
        CloseFile();
        OpenFile(date, sequence);
    }

    /// 앱을 껐다 켜면 같은 날 파일에 이어 쓴다. 이미 상한에 닿았으면 다음 번호로 넘어간다.
    private int StartingSequence(DateOnly date)
    {
        int sequence = 0;
        while (true)
        {
            var info = new FileInfo(Path.Combine(_directory, LogFilePolicy.FileName(date, sequence)));
            if (!info.Exists || info.Length < _maxFileBytes) return sequence;
            sequence++;
        }
    }

    private void OpenFile(DateOnly date, int sequence)
    {
        string path = Path.Combine(_directory, LogFilePolicy.FileName(date, sequence));
        bool isNew = !File.Exists(path);

        // 엑셀이 한글을 깨뜨리지 않도록 새 파일에는 BOM 을 남긴다.
        _stream = new StreamWriter(
            new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: isNew))
        {
            AutoFlush = false,
        };

        _openDate = date;
        _openSequence = sequence;
        _openBytes = isNew ? 0 : new FileInfo(path).Length;

        if (isNew)
        {
            Write(date, LogCsvLine.Header);
        }
    }

    private void CloseFile()
    {
        if (_stream == null) return;

        try
        {
            _stream.Flush();
            _stream.Dispose();
        }
        catch
        {
            // 닫기 실패로 앱이 멈추지는 않는다.
        }

        _stream = null;
    }
}
