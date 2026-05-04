using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Backend;

namespace AnimatronicsControlCenter.Core.Interfaces;

public interface IBackendServerCatalogClient
{
    Task<BackendFetchResult<BackendStoreDetailResponse>> GetStoreDetailAsync(
        string storeId,
        CancellationToken cancellationToken);

    Task<BackendFetchResult<BackendStoreListResponse>> GetStoreListAsync(
        string countryCode,
        CancellationToken cancellationToken);

    Task<BackendSendResult> UpdatePcMetadataAsync(
        string storeId,
        string pcId,
        BackendPcUpdateRequest request,
        CancellationToken cancellationToken);

    Task<BackendFetchResult<BackendStoreCreateResponse>> CreateStoreAsync(
        BackendStoreCreateRequest request,
        CancellationToken cancellationToken);

    Task<BackendSendResult> UpdateStoreAsync(
        string storeId,
        BackendStoreUpdateRequest request,
        CancellationToken cancellationToken);

    Task<BackendFetchResult<BackendPcAddResponse>> CreatePcAsync(
        string storeId,
        BackendPcCreateRequest request,
        CancellationToken cancellationToken);

    Task<BackendFetchResult<BackendObjectCreateResponse>> CreateObjectAsync(
        string storeId,
        string pcId,
        BackendObjectCreateRequest request,
        CancellationToken cancellationToken);

    Task<BackendSendResult> UpdateObjectAsync(
        string objectId,
        BackendObjectUpdateRequest request,
        CancellationToken cancellationToken);
}

public sealed record BackendFetchResult<T>(
    bool Success,
    int? StatusCode,
    string Message,
    T? Data);
