using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendServerCatalogClient : IBackendServerCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private readonly IBackendTrafficTap? _trafficTap;

    public BackendServerCatalogClient(HttpClient httpClient, ISettingsService settingsService, IBackendTrafficTap? trafficTap = null)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
        _trafficTap = trafficTap;
    }

    public async Task<BackendFetchResult<BackendStoreDetailResponse>> GetStoreDetailAsync(
        string storeId,
        CancellationToken cancellationToken)
    {
        if (!BackendHttpRequest.TryCreateUri(_settingsService, $"/v1/service/stores/{Uri.EscapeDataString(storeId)}/detail", out Uri uri, out string message))
        {
            return new BackendFetchResult<BackendStoreDetailResponse>(false, null, message, null);
        }

        try
        {
            using HttpRequestMessage request = BackendHttpRequest.Create(_settingsService, HttpMethod.Get, uri);
            Stopwatch stopwatch = StartTraffic(request.Method, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            RecordResponse(request.Method, uri, response, stopwatch, body);
            if (!response.IsSuccessStatusCode)
            {
                return new BackendFetchResult<BackendStoreDetailResponse>(false, (int)response.StatusCode, body, null);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return new BackendFetchResult<BackendStoreDetailResponse>(false, (int)response.StatusCode, "Backend response body is empty.", null);
            }

            var data = JsonSerializer.Deserialize<BackendStoreDetailResponse>(body, BackendHttpRequest.JsonOptions);
            return data is null
                ? new BackendFetchResult<BackendStoreDetailResponse>(false, (int)response.StatusCode, "Backend response body is invalid.", null)
                : new BackendFetchResult<BackendStoreDetailResponse>(true, (int)response.StatusCode, "OK", data);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            RecordError(HttpMethod.Get, uri, ex.Message);
            return new BackendFetchResult<BackendStoreDetailResponse>(false, null, ex.Message, null);
        }
    }

    public async Task<BackendFetchResult<BackendStoreListResponse>> GetStoreListAsync(
        string countryCode,
        CancellationToken cancellationToken)
    {
        string path = $"/v1/service/stores?country_code={Uri.EscapeDataString(countryCode)}";
        if (!BackendHttpRequest.TryCreateUri(_settingsService, path, out Uri uri, out string message))
        {
            return new BackendFetchResult<BackendStoreListResponse>(false, null, message, null);
        }

        try
        {
            using HttpRequestMessage request = BackendHttpRequest.Create(_settingsService, HttpMethod.Get, uri);
            Stopwatch stopwatch = StartTraffic(request.Method, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            RecordResponse(request.Method, uri, response, stopwatch, body);
            if (!response.IsSuccessStatusCode)
            {
                return new BackendFetchResult<BackendStoreListResponse>(false, (int)response.StatusCode, body, null);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return new BackendFetchResult<BackendStoreListResponse>(false, (int)response.StatusCode, "Backend response body is empty.", null);
            }

            var data = JsonSerializer.Deserialize<BackendStoreListResponse>(body, BackendHttpRequest.JsonOptions);
            return data is null
                ? new BackendFetchResult<BackendStoreListResponse>(false, (int)response.StatusCode, "Backend response body is invalid.", null)
                : new BackendFetchResult<BackendStoreListResponse>(true, (int)response.StatusCode, "OK", data);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            RecordError(HttpMethod.Get, uri, ex.Message);
            return new BackendFetchResult<BackendStoreListResponse>(false, null, ex.Message, null);
        }
    }

    public async Task<BackendSendResult> UpdatePcMetadataAsync(
        string storeId,
        string pcId,
        BackendPcUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!BackendHttpRequest.TryCreateUri(_settingsService, $"/v1/service/stores/{Uri.EscapeDataString(storeId)}/pcs/{Uri.EscapeDataString(pcId)}", out Uri uri, out string message))
        {
            return new BackendSendResult(false, null, message);
        }

        try
        {
            using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Put, uri);
            httpRequest.Content = BackendHttpRequest.JsonContent(request);
            Stopwatch stopwatch = StartTraffic(httpRequest.Method, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            RecordResponse(httpRequest.Method, uri, response, stopwatch, body);
            return response.IsSuccessStatusCode
                ? new BackendSendResult(true, (int)response.StatusCode, "OK")
                : new BackendSendResult(false, (int)response.StatusCode, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            RecordError(HttpMethod.Put, uri, ex.Message);
            return new BackendSendResult(false, null, ex.Message);
        }
    }

    public async Task<BackendFetchResult<BackendStoreCreateResponse>> CreateStoreAsync(
        BackendStoreCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!BackendHttpRequest.TryCreateUri(_settingsService, "/v1/service/stores", out Uri uri, out string message))
            return new BackendFetchResult<BackendStoreCreateResponse>(false, null, message, null);

        try
        {
            using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Post, uri);
            httpRequest.Content = BackendHttpRequest.JsonContent(request);
            Stopwatch stopwatch = StartTraffic(httpRequest.Method, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            RecordResponse(httpRequest.Method, uri, response, stopwatch, body);
            if (!response.IsSuccessStatusCode)
                return new BackendFetchResult<BackendStoreCreateResponse>(false, (int)response.StatusCode, body, null);
            if (string.IsNullOrWhiteSpace(body))
                return new BackendFetchResult<BackendStoreCreateResponse>(false, (int)response.StatusCode, "Backend response body is empty.", null);
            var data = JsonSerializer.Deserialize<BackendStoreCreateResponse>(body, BackendHttpRequest.JsonOptions);
            return data is null
                ? new BackendFetchResult<BackendStoreCreateResponse>(false, (int)response.StatusCode, "Backend response body is invalid.", null)
                : new BackendFetchResult<BackendStoreCreateResponse>(true, (int)response.StatusCode, "OK", data);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            RecordError(HttpMethod.Post, uri, ex.Message);
            return new BackendFetchResult<BackendStoreCreateResponse>(false, null, ex.Message, null);
        }
    }

    public async Task<BackendSendResult> UpdateStoreAsync(
        string storeId,
        BackendStoreUpdateRequest request,
        CancellationToken cancellationToken)
    {
        string path = $"/v1/service/stores/{Uri.EscapeDataString(storeId)}";
        if (!BackendHttpRequest.TryCreateUri(_settingsService, path, out Uri uri, out string message))
            return new BackendSendResult(false, null, message);

        try
        {
            using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Put, uri);
            httpRequest.Content = BackendHttpRequest.JsonContent(request);
            Stopwatch stopwatch = StartTraffic(httpRequest.Method, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            RecordResponse(httpRequest.Method, uri, response, stopwatch, body);
            return response.IsSuccessStatusCode
                ? new BackendSendResult(true, (int)response.StatusCode, "OK")
                : new BackendSendResult(false, (int)response.StatusCode, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            RecordError(HttpMethod.Put, uri, ex.Message);
            return new BackendSendResult(false, null, ex.Message);
        }
    }

    public async Task<BackendFetchResult<BackendPcAddResponse>> CreatePcAsync(
        string storeId,
        BackendPcCreateRequest request,
        CancellationToken cancellationToken)
    {
        string path = $"/v1/service/stores/{Uri.EscapeDataString(storeId)}/pcs";
        if (!BackendHttpRequest.TryCreateUri(_settingsService, path, out Uri uri, out string message))
            return new BackendFetchResult<BackendPcAddResponse>(false, null, message, null);

        try
        {
            using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Post, uri);
            httpRequest.Content = BackendHttpRequest.JsonContent(request);
            Stopwatch stopwatch = StartTraffic(httpRequest.Method, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            RecordResponse(httpRequest.Method, uri, response, stopwatch, body);
            if (!response.IsSuccessStatusCode)
                return new BackendFetchResult<BackendPcAddResponse>(false, (int)response.StatusCode, body, null);
            if (string.IsNullOrWhiteSpace(body))
                return new BackendFetchResult<BackendPcAddResponse>(false, (int)response.StatusCode, "Backend response body is empty.", null);
            var data = JsonSerializer.Deserialize<BackendPcAddResponse>(body, BackendHttpRequest.JsonOptions);
            return data is null
                ? new BackendFetchResult<BackendPcAddResponse>(false, (int)response.StatusCode, "Backend response body is invalid.", null)
                : new BackendFetchResult<BackendPcAddResponse>(true, (int)response.StatusCode, "OK", data);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            RecordError(HttpMethod.Post, uri, ex.Message);
            return new BackendFetchResult<BackendPcAddResponse>(false, null, ex.Message, null);
        }
    }

    public async Task<BackendFetchResult<BackendObjectCreateResponse>> CreateObjectAsync(
        string storeId,
        string pcId,
        BackendObjectCreateRequest request,
        CancellationToken cancellationToken)
    {
        string path = $"/v1/service/stores/{Uri.EscapeDataString(storeId)}/pcs/{Uri.EscapeDataString(pcId)}/objects";
        if (!BackendHttpRequest.TryCreateUri(_settingsService, path, out Uri uri, out string message))
            return new BackendFetchResult<BackendObjectCreateResponse>(false, null, message, null);

        try
        {
            using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Post, uri);
            httpRequest.Content = BackendHttpRequest.JsonContent(request);
            Stopwatch stopwatch = StartTraffic(httpRequest.Method, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            RecordResponse(httpRequest.Method, uri, response, stopwatch, body);
            if (!response.IsSuccessStatusCode)
                return new BackendFetchResult<BackendObjectCreateResponse>(false, (int)response.StatusCode, body, null);
            if (string.IsNullOrWhiteSpace(body))
                return new BackendFetchResult<BackendObjectCreateResponse>(false, (int)response.StatusCode, "Backend response body is empty.", null);
            var data = JsonSerializer.Deserialize<BackendObjectCreateResponse>(body, BackendHttpRequest.JsonOptions);
            return data is null
                ? new BackendFetchResult<BackendObjectCreateResponse>(false, (int)response.StatusCode, "Backend response body is invalid.", null)
                : new BackendFetchResult<BackendObjectCreateResponse>(true, (int)response.StatusCode, "OK", data);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            RecordError(HttpMethod.Post, uri, ex.Message);
            return new BackendFetchResult<BackendObjectCreateResponse>(false, null, ex.Message, null);
        }
    }

    public async Task<BackendSendResult> UpdateObjectAsync(
        string objectId,
        BackendObjectUpdateRequest request,
        CancellationToken cancellationToken)
    {
        string path = $"/v1/service/objects/{Uri.EscapeDataString(objectId)}";
        if (!BackendHttpRequest.TryCreateUri(_settingsService, path, out Uri uri, out string message))
            return new BackendSendResult(false, null, message);

        try
        {
            using HttpRequestMessage httpRequest = BackendHttpRequest.Create(_settingsService, HttpMethod.Put, uri);
            httpRequest.Content = BackendHttpRequest.JsonContent(request);
            Stopwatch stopwatch = StartTraffic(httpRequest.Method, uri);
            using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            RecordResponse(httpRequest.Method, uri, response, stopwatch, body);
            return response.IsSuccessStatusCode
                ? new BackendSendResult(true, (int)response.StatusCode, "OK")
                : new BackendSendResult(false, (int)response.StatusCode, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            RecordError(HttpMethod.Put, uri, ex.Message);
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
