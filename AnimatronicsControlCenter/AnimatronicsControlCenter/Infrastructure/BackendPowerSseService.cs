using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Protocol;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendPowerSseService : IBackendPowerSseService, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly IBackendTrafficTap _trafficTap;
    private readonly ISerialService? _serialService;
    private readonly HttpClient _httpClient;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;
    private List<Task> _tasks = new();

    public BackendPowerSseService(ISettingsService settingsService, IBackendTrafficTap trafficTap, ISerialService serialService)
        : this(settingsService, trafficTap, serialService, new SocketsHttpHandler())
    {
    }

    public BackendPowerSseService(ISettingsService settingsService, IBackendTrafficTap trafficTap, HttpMessageHandler handler)
        : this(settingsService, trafficTap, null, handler)
    {
    }

    public BackendPowerSseService(ISettingsService settingsService, IBackendTrafficTap trafficTap, ISerialService? serialService, HttpMessageHandler handler)
    {
        _settingsService = settingsService;
        _trafficTap = trafficTap;
        _serialService = serialService;
        _httpClient = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public void Start()
    {
        if (!_settingsService.IsBackendSyncEnabled)
        {
            return;
        }

        lock (_lock)
        {
            if (_cts is { IsCancellationRequested: false })
            {
                return;
            }

            var objectIds = _settingsService.BackendDeviceObjectMappings.Values
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (objectIds.Count == 0)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _tasks = objectIds
                .Select(objectId => Task.Run(() => SubscribeObjectPowerAsync(objectId, _cts.Token)))
                .ToList();
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _tasks = new List<Task>();
        }
    }

    public void Dispose()
    {
        Stop();
        _httpClient.Dispose();
    }

    private async Task SubscribeObjectPowerAsync(string objectId, CancellationToken cancellationToken)
    {
        if (!BackendHttpRequest.TryCreateUri(_settingsService, $"/v1/service/objects/{Uri.EscapeDataString(objectId)}/power", out Uri uri, out string message))
        {
            RecordError(HttpMethod.Get, new Uri("about:blank"), message);
            return;
        }

        try
        {
            using HttpRequestMessage request = BackendHttpRequest.Create(_settingsService, HttpMethod.Get, uri);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            Stopwatch stopwatch = StartTraffic(request.Method, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                RecordResponse(request.Method, uri, response, stopwatch, body);
                return;
            }

            RecordResponse(request.Method, uri, response, stopwatch, "SSE connected");

            await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await ReadSseEventsAsync(request.Method, uri, reader, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
        {
            RecordError(HttpMethod.Get, uri, ex.Message);
        }
    }

    private async Task ReadSseEventsAsync(HttpMethod method, Uri uri, StreamReader reader, CancellationToken cancellationToken)
    {
        var dataLines = new List<string>();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (line.Length == 0)
            {
                RecordDataEvent(method, uri, dataLines);
                dataLines.Clear();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                dataLines.Add(line[5..].TrimStart());
            }
        }

        RecordDataEvent(method, uri, dataLines);
    }

    private void RecordDataEvent(HttpMethod method, Uri uri, List<string> dataLines)
    {
        if (dataLines.Count == 0)
        {
            return;
        }

        string payload = string.Join("\n", dataLines);
        _trafficTap.RecordResponse(method, uri, 200, TimeSpan.Zero, $"SSE data: {payload}", DateTimeOffset.Now);
        _ = ForwardPowerCommandAsync(payload);
    }

    private async Task ForwardPowerCommandAsync(string payload)
    {
        try
        {
            if (_serialService is null)
            {
                return;
            }

            if (!TryParsePowerCommand(payload, out string objectId, out BinaryPowerAction action))
            {
                return;
            }

            int? deviceId = ResolveDeviceId(objectId);
            if (deviceId is null)
            {
                return;
            }

            byte[] packet = BinarySerializer.EncodePowerCtrl(BinaryProtocolConst.HostId, (byte)deviceId.Value, action);
            await _serialService.SendBinaryCommandAsync(deviceId.Value, packet).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RecordError(HttpMethod.Get, new Uri("about:blank"), $"POWER_CTRL forwarding failed: {ex.Message}");
        }
    }

    private bool TryParsePowerCommand(string payload, out string objectId, out BinaryPowerAction action)
    {
        objectId = string.Empty;
        action = default;

        try
        {
            using JsonDocument outer = JsonDocument.Parse(payload);
            JsonElement root = outer.RootElement;
            if (root.TryGetProperty("eventType", out JsonElement eventTypeElement) &&
                !string.Equals(eventTypeElement.GetString(), "COMMAND", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            JsonElement commandRoot = root;
            JsonDocument? inner = null;
            try
            {
                if (root.TryGetProperty("data", out JsonElement dataElement) &&
                    dataElement.ValueKind == JsonValueKind.String)
                {
                    inner = JsonDocument.Parse(dataElement.GetString() ?? string.Empty);
                    commandRoot = inner.RootElement;
                }

                if (!TryReadString(commandRoot, "object_id", out objectId))
                {
                    return false;
                }

                if (TryReadString(commandRoot, "power_action", out string powerAction) ||
                    TryReadString(commandRoot, "power_status", out powerAction))
                {
                    return TryParsePowerAction(powerAction, out action);
                }

                return false;
            }
            finally
            {
                inner?.Dispose();
            }
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private int? ResolveDeviceId(string objectId)
    {
        foreach (KeyValuePair<int, string> mapping in _settingsService.BackendDeviceObjectMappings)
        {
            if (string.Equals(mapping.Value, objectId, StringComparison.Ordinal))
            {
                return mapping.Key;
            }
        }

        return null;
    }

    private static bool TryReadString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryParsePowerAction(string value, out BinaryPowerAction action)
    {
        string normalized = value.Trim();
        action = normalized.ToUpperInvariant() switch
        {
            "OFF" => BinaryPowerAction.Off,
            "ON" => BinaryPowerAction.On,
            "REBOOT" => BinaryPowerAction.Reboot,
            _ => default,
        };

        return normalized.Equals("OFF", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("ON", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("REBOOT", StringComparison.OrdinalIgnoreCase);
    }

    private Stopwatch StartTraffic(HttpMethod method, Uri uri)
    {
        _trafficTap.RecordRequest(method, uri, DateTimeOffset.Now);
        return Stopwatch.StartNew();
    }

    private void RecordResponse(HttpMethod method, Uri uri, HttpResponseMessage response, Stopwatch stopwatch, string message)
    {
        stopwatch.Stop();
        _trafficTap.RecordResponse(method, uri, (int)response.StatusCode, stopwatch.Elapsed, message, DateTimeOffset.Now);
    }

    private void RecordError(HttpMethod method, Uri uri, string message)
    {
        _trafficTap.RecordError(method, uri, TimeSpan.Zero, message, DateTimeOffset.Now);
    }
}
