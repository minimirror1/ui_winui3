using System;
using System.Collections.Generic;
using System.Net.Http;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Utilities;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendTrafficTap
{
    event EventHandler? TrafficChanged;

    void RecordRequest(HttpMethod method, Uri uri, DateTimeOffset timestamp);

    void RecordResponse(HttpMethod method, Uri uri, int statusCode, TimeSpan duration, string message, DateTimeOffset timestamp);

    void RecordError(HttpMethod method, Uri uri, TimeSpan duration, string message, DateTimeOffset timestamp);

    BackendTrafficSnapshot GetSnapshot(DateTimeOffset now);

    IReadOnlyList<BackendTrafficEntry> GetEntries();
}
