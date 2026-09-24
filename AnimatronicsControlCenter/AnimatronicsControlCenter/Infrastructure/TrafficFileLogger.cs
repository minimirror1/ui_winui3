using System;
using System.Collections.Concurrent;
using System.Threading;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Utilities;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed record TrafficFileLoggerOptions(long MaxFileBytes, int QueueCapacity, bool IncludeRaw);

/// 기록기가 넘겨준 송수신을 파일에 남긴다.
/// 통신 스레드가 하는 일은 대기줄에 넣는 것까지이고, 해독·글자 변환·파일 쓰기는 기록 스레드가 맡는다.
public sealed class TrafficFileLogger : IDisposable
{
    private readonly ILogSink _sink;
    private readonly BlockingCollection<SerialTrafficEntry> _queue;
    private readonly Thread _writer;

    // 파일에 아직 표시하지 못한 버림과, 살아있는 동안의 총 버림을 따로 센다.
    // 기록 스레드가 앞의 것을 가져가 0으로 만들기 때문에, 하나로 합치면 총계를 읽을 수 없다.
    private int _pendingDrops;
    private int _totalDrops;
    private volatile bool _enabled = true;
    private volatile bool _includeRaw;

    public TrafficFileLogger(string directory, TrafficFileLoggerOptions options)
        : this(new FileLogSink(directory, options.MaxFileBytes), options)
    {
    }

    public TrafficFileLogger(ILogSink sink, TrafficFileLoggerOptions options)
    {
        _sink = sink;
        _queue = new BlockingCollection<SerialTrafficEntry>(
            new ConcurrentQueue<SerialTrafficEntry>(), Math.Max(1, options.QueueCapacity));

        _includeRaw = options.IncludeRaw;

        _writer = new Thread(WriteLoop) { IsBackground = true, Name = "traffic-file-log" };
        _writer.Start();
    }

    /// 기록을 남길지. 끄면 대기줄에 넣지도 않는다.
    public bool IsEnabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// 패킷 원문(hex)까지 남길지. 기본은 꺼짐이고, 켜면 줄 길이가 배로 늘어난다.
    public bool IncludeRaw
    {
        get => _includeRaw;
        set => _includeRaw = value;
    }

    /// 살아있는 동안 버린 줄의 총 수. 대기줄이 찼을 때만 늘어난다.
    public int DroppedCount => Volatile.Read(ref _totalDrops);

    /// 기록기를 구독한다. 이 뒤로 오는 송수신이 파일에 남는다.
    public void Subscribe(ISerialTrafficTap tap)
    {
        if (tap != null) tap.EntryRecorded += OnEntryRecorded;
    }

    public void Unsubscribe(ISerialTrafficTap tap)
    {
        if (tap != null) tap.EntryRecorded -= OnEntryRecorded;
    }

    /// 통신 스레드에서 불린다. 담아두기만 하고 즉시 돌아온다.
    public void Record(SerialTrafficEntry entry)
    {
        if (entry == null || !_enabled) return;

        // 기다리지 않는다. 자리가 없으면 버린다 - 통신을 세우느니 기록 몇 줄을 잃는 쪽이 맞다.
        if (!_queue.TryAdd(entry))
        {
            Interlocked.Increment(ref _pendingDrops);
            Interlocked.Increment(ref _totalDrops);
        }
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _writer.Join();
        _sink.Dispose();
        _queue.Dispose();
    }

    private void OnEntryRecorded(object? sender, SerialTrafficEntry entry) => Record(entry);

    private void WriteLoop()
    {
        foreach (SerialTrafficEntry entry in _queue.GetConsumingEnumerable())
        {
            try
            {
                WriteOne(entry);
            }
            catch
            {
                // 기록이 실패해도 앱은 계속 돌아야 한다.
            }

            if (_queue.Count == 0) _sink.Flush();
        }
    }

    private void WriteOne(SerialTrafficEntry entry)
    {
        DateOnly date = DateOnly.FromDateTime(entry.Timestamp.Date);

        // 버린 줄이 있었다면 기록에 구멍이 있다는 표시를 먼저 남긴다.
        int dropped = Interlocked.Exchange(ref _pendingDrops, 0);
        if (dropped > 0)
        {
            _sink.Write(date, $"# 대기줄이 차서 {dropped}줄 버림");
        }

        _sink.Write(date, LogCsvLine.Format(LogRowBuilder.FromEntry(entry, _includeRaw)));
    }
}
