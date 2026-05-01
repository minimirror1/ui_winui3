using AnimatronicsControlCenter.Core.Backend;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendServerCatalogClient
{
    Task<BackendFetchResult<BackendStoreDetailResponse>> GetStoreDetailAsync(
        string storeId,
        CancellationToken cancellationToken);

    Task<BackendSendResult> UpdatePcMetadataAsync(
        string storeId,
        string pcId,
        BackendPcUpdateRequest request,
        CancellationToken cancellationToken);
}

public sealed record BackendFetchResult<T>(
    bool Success,
    int? StatusCode,
    string Message,
    T? Data);
