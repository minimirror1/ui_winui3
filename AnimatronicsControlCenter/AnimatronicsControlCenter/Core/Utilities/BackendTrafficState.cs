using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Utilities;

public readonly record struct BackendTrafficSnapshot(
    bool IsServerOnline,
    bool IsUplinkActive,
    bool IsDownlinkActive,
    DateTimeOffset? LastSuccessAt,
    DateTimeOffset? LastFailureAt,
    string? LastErrorMessage);

public sealed class BackendTrafficState
{
    public static readonly TimeSpan ActivityWindow = TimeSpan.FromMilliseconds(300);
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);
    public const int MaxEntries = 100;

    private readonly List<BackendTrafficEntry> _entries = new();

    public DateTimeOffset? LastUplinkAt { get; private set; }

    public DateTimeOffset? LastDownlinkAt { get; private set; }

    public DateTimeOffset? LastSuccessAt { get; private set; }

    public DateTimeOffset? LastFailureAt { get; private set; }

    public string? LastErrorMessage { get; private set; }

    public void RecordRequest(HttpMethod method, Uri uri, DateTimeOffset timestamp)
    {
        LastUplinkAt = timestamp;
        AddEntry(new BackendTrafficEntry(timestamp, BackendTrafficPhase.Request, method, uri.AbsolutePath, null, null, "Request sent"));
    }

    public void RecordResponse(HttpMethod method, Uri uri, int statusCode, TimeSpan duration, string message, DateTimeOffset timestamp)
    {
        LastDownlinkAt = timestamp;
        if (statusCode is >= 200 and <= 299)
        {
            LastSuccessAt = timestamp;
        }
        else
        {
            LastFailureAt = timestamp;
            LastErrorMessage = message;
        }

        AddEntry(new BackendTrafficEntry(timestamp, BackendTrafficPhase.Response, method, uri.AbsolutePath, statusCode, duration, message));
    }

    public void RecordError(HttpMethod method, Uri uri, TimeSpan duration, string message, DateTimeOffset timestamp)
    {
        LastDownlinkAt = timestamp;
        LastFailureAt = timestamp;
        LastErrorMessage = message;
        AddEntry(new BackendTrafficEntry(timestamp, BackendTrafficPhase.Error, method, uri.AbsolutePath, null, duration, message));
    }

    public BackendTrafficSnapshot GetSnapshot(DateTimeOffset now)
        => new(
            IsServerOnline: LastSuccessAt.HasValue && now - LastSuccessAt.Value <= OnlineWindow,
            IsUplinkActive: IsWithinActivityWindow(LastUplinkAt, now),
            IsDownlinkActive: IsWithinActivityWindow(LastDownlinkAt, now),
            LastSuccessAt: LastSuccessAt,
            LastFailureAt: LastFailureAt,
            LastErrorMessage: LastErrorMessage);

    public IReadOnlyList<BackendTrafficEntry> GetEntries()
        => _entries.ToList();

    private void AddEntry(BackendTrafficEntry entry)
    {
        _entries.Add(entry);
        if (_entries.Count > MaxEntries)
        {
            _entries.RemoveRange(0, _entries.Count - MaxEntries);
        }
    }

    private static bool IsWithinActivityWindow(DateTimeOffset? lastActivityAt, DateTimeOffset now)
        => lastActivityAt.HasValue && now - lastActivityAt.Value <= ActivityWindow;
}
