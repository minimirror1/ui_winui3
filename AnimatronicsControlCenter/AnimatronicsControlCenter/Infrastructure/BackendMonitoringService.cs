using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendMonitoringService : IBackendMonitoringService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly IBackendObjectIdResolver _objectIdResolver;
    private readonly IBackendTrafficTap? _trafficTap;

    public BackendMonitoringService(
        HttpClient httpClient,
        ISettingsService settingsService,
        IBackendObjectIdResolver objectIdResolver,
        IBackendTrafficTap? trafficTap = null)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _objectIdResolver = objectIdResolver;
        _trafficTap = trafficTap;
    }

    public async Task<BackendSendResult> SendObjectLogAsync(Device device, CancellationToken cancellationToken)
    {
        if (!_settingsService.IsBackendSyncEnabled)
        {
            return new BackendSendResult(false, null, "Backend sync disabled.");
        }

        string? objectId = _objectIdResolver.ResolveObjectId(device.Id);
        if (objectId is null)
        {
            return new BackendSendResult(false, null, $"No backend object mapping for device {device.Id}.");
        }

        if (!BackendHttpRequest.TryCreateUri(_settingsService, $"/v1/service/objects/{Uri.EscapeDataString(objectId)}/logs", out Uri uri, out string message))
        {
            return new BackendSendResult(false, null, message);
        }

        BackendObjectLogRequest log = BackendDeviceMapper.CreateObjectLog(device);

        try
        {
            using HttpRequestMessage request = BackendHttpRequest.Create(_settingsService, HttpMethod.Post, uri);
            request.Content = BackendHttpRequest.JsonContent(log);
            Stopwatch stopwatch = StartTraffic(request.Method, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            RecordResponse(request.Method, uri, response, stopwatch, body);
            return response.IsSuccessStatusCode
                ? new BackendSendResult(true, (int)response.StatusCode, "OK")
                : new BackendSendResult(false, (int)response.StatusCode, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            RecordError(HttpMethod.Post, uri, ex.Message);
            return new BackendSendResult(false, null, ex.Message);
        }
    }

    private Stopwatch StartTraffic(HttpMethod method, Uri uri)
    {
        _trafficTap?.RecordRequest(method, uri, DateTimeOffset.Now);
        return Stopwatch.StartNew();
    }

    private void RecordResponse(HttpMethod method, Uri uri, HttpResponseMessage response, Stopwatch stopwatch, string body)
    {
        stopwatch.Stop();
        string message = response.IsSuccessStatusCode ? "OK" : body;
        _trafficTap?.RecordResponse(method, uri, (int)response.StatusCode, stopwatch.Elapsed, message, DateTimeOffset.Now);
    }

    private void RecordError(HttpMethod method, Uri uri, string message)
    {
        _trafficTap?.RecordError(method, uri, TimeSpan.Zero, message, DateTimeOffset.Now);
    }
}
