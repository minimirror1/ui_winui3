using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendMonitoringService : IBackendMonitoringService
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly IBackendObjectIdResolver _objectIdResolver;

    public BackendMonitoringService(
        HttpClient httpClient,
        ISettingsService settingsService,
        IBackendObjectIdResolver objectIdResolver)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _objectIdResolver = objectIdResolver;
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

        var log = new BackendObjectLogRequest(
            PowerStatus: "OFF",
            OperationStatus: device.MotionState == MotionState.Playing ? "PLAY" : "STOP",
            PowerConsumption: null,
            ErrorData: Array.Empty<BackendErrorData>());

        try
        {
            using HttpRequestMessage request = BackendHttpRequest.Create(_settingsService, HttpMethod.Post, uri);
            request.Content = BackendHttpRequest.JsonContent(log);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            return response.IsSuccessStatusCode
                ? new BackendSendResult(true, (int)response.StatusCode, "OK")
                : new BackendSendResult(false, (int)response.StatusCode, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new BackendSendResult(false, null, ex.Message);
        }
    }
}
