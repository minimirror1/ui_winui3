using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnimatronicsControlCenter.UI.ViewModels;

public partial class BackendSettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IBackendServerCatalogClient _serverCatalogClient;
    private BackendServerSnapshot? _lastServerSnapshot;
    private BackendStoreDetailResponse? _lastFetchedStoreDetail;

    [ObservableProperty] private string backendBaseUrl = string.Empty;
    [ObservableProperty] private string backendBearerToken = string.Empty;
    [ObservableProperty] private string backendStoreId = string.Empty;
    [ObservableProperty] private string backendStoreName = string.Empty;
    [ObservableProperty] private string backendStoreCountryCode = string.Empty;
    [ObservableProperty] private string backendPcId = string.Empty;
    [ObservableProperty] private string backendPcName = string.Empty;
    [ObservableProperty] private string backendSoftwareVersion = string.Empty;
    [ObservableProperty] private string backendDeviceObjectMappingsText = "{}";
    [ObservableProperty] private string backendDeviceObjectMappingsMessage = string.Empty;
    [ObservableProperty] private bool isBackendSyncEnabled;
    [ObservableProperty] private int backendSyncIntervalSeconds;
    [ObservableProperty] private string serverStatusMessage = string.Empty;
    [ObservableProperty] private string localStatusMessage = string.Empty;
    [ObservableProperty] private string storeCountryCodeComparisonMessage = string.Empty;
    [ObservableProperty] private string pcIdComparisonMessage = string.Empty;
    [ObservableProperty] private string swVersionComparisonMessage = string.Empty;
    [ObservableProperty] private string deviceObjectMappingsComparisonMessage = string.Empty;
    [ObservableProperty] private string? selectedCountryCode;
    [ObservableProperty] private BackendStoreSummaryResponse? selectedServerStore;
    [ObservableProperty] private BackendPcDetailResponse? selectedServerPc;
    [ObservableProperty] private bool isFetchingStoreList;

    public IReadOnlyList<string> AvailableCountryCodes { get; } = ["KR", "JP", "US", "CN", "GB"];
    public ObservableCollection<BackendStoreSummaryResponse> ServerStoreList { get; } = new();
    public ObservableCollection<BackendPcDetailResponse> ServerPcList { get; } = new();
    public ObservableCollection<BackendServerObjectSnapshot> ServerObjects { get; } = new();

    public BackendSettingsViewModel(ISettingsService settingsService, IBackendServerCatalogClient serverCatalogClient)
    {
        _settingsService = settingsService;
        _serverCatalogClient = serverCatalogClient;
        LoadFromSettings();
    }

    [RelayCommand]
    private void ApplyServerValues()
    {
        if (_lastServerSnapshot is null)
        {
            LocalStatusMessage = "서버 값을 먼저 조회해야 비교할 수 있습니다.";
            return;
        }

        BackendStoreId = _lastServerSnapshot.StoreId;
        BackendStoreName = _lastServerSnapshot.StoreName ?? string.Empty;
        BackendStoreCountryCode = _lastServerSnapshot.StoreCountryCode ?? string.Empty;
        BackendPcId = _lastServerSnapshot.PcId ?? string.Empty;
        BackendPcName = _lastServerSnapshot.PcName ?? BackendPcName;
        BackendSoftwareVersion = _lastServerSnapshot.SwVersion ?? BackendSoftwareVersion;
        LocalStatusMessage = "서버 값을 로컬 draft에 적용했습니다.";
    }

    [RelayCommand]
    private void CompareWithServer()
    {
        var result = BackendSettingsComparison.Compare(_lastServerSnapshot, CreateLocalSnapshot());
        LocalStatusMessage = result.SummaryMessage;
        StoreCountryCodeComparisonMessage = result.Fields.FirstOrDefault(field => field.FieldName == "StoreCountryCode")?.Message ?? string.Empty;
        PcIdComparisonMessage = result.Fields.FirstOrDefault(field => field.FieldName == "PcId")?.Message ?? string.Empty;
        SwVersionComparisonMessage = result.Fields.FirstOrDefault(field => field.FieldName == "SwVersion")?.Message ?? string.Empty;
        DeviceObjectMappingsComparisonMessage = result.Fields.FirstOrDefault(field => field.FieldName == "DeviceObjectMappings")?.Message ?? string.Empty;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryParseMappings(out Dictionary<int, string> mappings))
        {
            return;
        }

        _settingsService.IsBackendSyncEnabled = IsBackendSyncEnabled;
        _settingsService.BackendBaseUrl = BackendBaseUrl;
        _settingsService.BackendBearerToken = BackendBearerToken;
        _settingsService.BackendStoreId = BackendStoreId;
        _settingsService.BackendStoreName = BackendStoreName;
        _settingsService.BackendStoreCountryCode = BackendStoreCountryCode;
        _settingsService.BackendPcId = BackendPcId;
        _settingsService.BackendPcName = BackendPcName;
        _settingsService.BackendSoftwareVersion = BackendSoftwareVersion;
        _settingsService.BackendDeviceObjectMappings = mappings;
        _settingsService.BackendSyncIntervalSeconds = BackendSyncIntervalSeconds;
        _settingsService.Save();

        if (!string.IsNullOrWhiteSpace(BackendStoreId) && !string.IsNullOrWhiteSpace(BackendPcId))
        {
            BackendSendResult result = await _serverCatalogClient.UpdatePcMetadataAsync(
                BackendStoreId,
                BackendPcId,
                new BackendPcUpdateRequest(BackendPcName, BackendSoftwareVersion),
                CancellationToken.None);
            LocalStatusMessage = result.Success ? "저장했습니다." : $"저장했지만 PC metadata 보고에 실패했습니다. {result.Message}";
            return;
        }

        LocalStatusMessage = "저장했습니다.";
    }

    private void LoadFromSettings()
    {
        _settingsService.Load();
        BackendBaseUrl = _settingsService.BackendBaseUrl;
        BackendBearerToken = _settingsService.BackendBearerToken;
        BackendStoreId = _settingsService.BackendStoreId;
        BackendStoreName = _settingsService.BackendStoreName;
        BackendStoreCountryCode = _settingsService.BackendStoreCountryCode;
        BackendPcId = _settingsService.BackendPcId;
        BackendPcName = _settingsService.BackendPcName;
        BackendSoftwareVersion = _settingsService.BackendSoftwareVersion;
        BackendDeviceObjectMappingsText = JsonSerializer.Serialize(_settingsService.BackendDeviceObjectMappings);
        IsBackendSyncEnabled = _settingsService.IsBackendSyncEnabled;
        BackendSyncIntervalSeconds = _settingsService.BackendSyncIntervalSeconds;
    }

    private BackendLocalSettingsSnapshot CreateLocalSnapshot()
    {
        _ = TryParseMappings(out Dictionary<int, string> mappings);
        return new BackendLocalSettingsSnapshot(
            BackendStoreId,
            BackendStoreName,
            BackendStoreCountryCode,
            BackendPcId,
            BackendPcName,
            BackendSoftwareVersion,
            mappings);
    }

    partial void OnSelectedCountryCodeChanged(string? value)
    {
        ServerStoreList.Clear();
        SelectedServerStore = null;
        ServerPcList.Clear();
        SelectedServerPc = null;
        ServerObjects.Clear();
        _lastServerSnapshot = null;
        _lastFetchedStoreDetail = null;
        if (value is not null)
            _ = FetchStoreListAsync(value);
    }

    partial void OnSelectedServerPcChanged(BackendPcDetailResponse? value)
    {
        ServerObjects.Clear();
        _lastServerSnapshot = null;
        if (value is null || _lastFetchedStoreDetail is null) return;

        _lastServerSnapshot = new BackendServerSnapshot(
            _lastFetchedStoreDetail.StoreId,
            _lastFetchedStoreDetail.StoreName,
            _lastFetchedStoreDetail.CountryCode,
            value.PcId,
            value.PcName,
            value.SwVersion,
            value.Objects.Select(o => new BackendServerObjectSnapshot(o.Id, o.ObjectName)).ToArray());

        foreach (BackendServerObjectSnapshot obj in _lastServerSnapshot.Objects)
            ServerObjects.Add(obj);
    }

    partial void OnSelectedServerStoreChanged(BackendStoreSummaryResponse? value)
    {
        ServerPcList.Clear();
        SelectedServerPc = null;
        ServerObjects.Clear();
        _lastServerSnapshot = null;
        _lastFetchedStoreDetail = null;
        if (value is not null)
            _ = FetchStoreDetailForSelectionAsync(value.StoreId);
    }

    private async Task FetchStoreDetailForSelectionAsync(string storeId)
    {
        ServerStatusMessage = string.Empty;
        var result = await _serverCatalogClient.GetStoreDetailAsync(storeId, CancellationToken.None);
        if (!result.Success || result.Data is null)
        {
            ServerStatusMessage = result.Message;
            return;
        }
        _lastFetchedStoreDetail = result.Data;
        foreach (BackendPcDetailResponse pc in result.Data.Pcs)
            ServerPcList.Add(pc);
    }

    private async Task FetchStoreListAsync(string countryCode)
    {
        IsFetchingStoreList = true;
        ServerStatusMessage = string.Empty;
        var result = await _serverCatalogClient.GetStoreListAsync(countryCode, CancellationToken.None);
        IsFetchingStoreList = false;
        if (!result.Success || result.Data is null)
        {
            ServerStatusMessage = result.Message;
            return;
        }
        if (result.Data.Stores.Count == 0)
        {
            ServerStatusMessage = "해당 국가에 등록된 스토어가 없습니다.";
            return;
        }
        foreach (BackendStoreSummaryResponse store in result.Data.Stores)
            ServerStoreList.Add(store);
    }

    private bool TryParseMappings(out Dictionary<int, string> mappings)
    {
        mappings = new Dictionary<int, string>();
        BackendDeviceObjectMappingsMessage = string.Empty;
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<int, string>>(BackendDeviceObjectMappingsText);
            mappings = parsed ?? new Dictionary<int, string>();
            return true;
        }
        catch (JsonException ex)
        {
            BackendDeviceObjectMappingsMessage = $"Mapping JSON 파싱 실패: {ex.Message}";
            LocalStatusMessage = BackendDeviceObjectMappingsMessage;
            return false;
        }
    }
}
