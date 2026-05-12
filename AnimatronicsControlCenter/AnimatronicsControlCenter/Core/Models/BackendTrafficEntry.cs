using System;
using System.Net.Http;

namespace AnimatronicsControlCenter.Core.Models;

public enum BackendTrafficPhase
{
    Request,
    Response,
    Error
}

public sealed record BackendTrafficEntry(
    DateTimeOffset Timestamp,
    BackendTrafficPhase Phase,
    HttpMethod Method,
    string Path,
    int? StatusCode,
    TimeSpan? Duration,
    string Message);
