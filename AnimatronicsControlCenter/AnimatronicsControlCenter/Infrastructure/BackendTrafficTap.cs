using System;
using System.Collections.Generic;
using System.Net.Http;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Utilities;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendTrafficTap : IBackendTrafficTap
{
    private readonly BackendTrafficState _state = new();
    private readonly object _lock = new();

    public event EventHandler? TrafficChanged;

    public void RecordRequest(HttpMethod method, Uri uri, DateTimeOffset timestamp)
    {
        lock (_lock)
        {
            _state.RecordRequest(method, uri, timestamp);
        }

        TrafficChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordResponse(HttpMethod method, Uri uri, int statusCode, TimeSpan duration, string message, DateTimeOffset timestamp)
    {
        lock (_lock)
        {
            _state.RecordResponse(method, uri, statusCode, duration, message, timestamp);
        }

        TrafficChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordError(HttpMethod method, Uri uri, TimeSpan duration, string message, DateTimeOffset timestamp)
    {
        lock (_lock)
        {
            _state.RecordError(method, uri, duration, message, timestamp);
        }

        TrafficChanged?.Invoke(this, EventArgs.Empty);
    }

    public BackendTrafficSnapshot GetSnapshot(DateTimeOffset now)
    {
        lock (_lock)
        {
            return _state.GetSnapshot(now);
        }
    }

    public IReadOnlyList<BackendTrafficEntry> GetEntries()
    {
        lock (_lock)
        {
            return _state.GetEntries();
        }
    }
}
