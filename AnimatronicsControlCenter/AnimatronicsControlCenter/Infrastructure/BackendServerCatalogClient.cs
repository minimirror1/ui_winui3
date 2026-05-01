using System.Text.Json;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendServerCatalogClient : IBackendServerCatalogClient
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;

    public BackendServerCatalogClient(HttpClient httpClient, ISettingsService settingsService)
    {
        _httpClient = httpClient;
        _settingsService = settingsService;
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
            using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
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
            return new BackendFetchResult<BackendStoreDetailResponse>(false, null, ex.Message, null);
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
            using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
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
