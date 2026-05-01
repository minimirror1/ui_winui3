namespace AnimatronicsControlCenter.Core.Backend;

public sealed record BackendServerSnapshot(
    string StoreId,
    string? StoreName,
    string? StoreCountryCode,
    string? PcId,
    string? PcName,
    string? SwVersion,
    IReadOnlyList<BackendServerObjectSnapshot> Objects);

public sealed record BackendServerObjectSnapshot(string ObjectId, string? ObjectName);

public sealed record BackendLocalSettingsSnapshot(
    string StoreId,
    string StoreName,
    string? StoreCountryCode,
    string PcId,
    string PcName,
    string SwVersion,
    IReadOnlyDictionary<int, string> DeviceObjectMappings);

public sealed record BackendFieldComparison(
    string FieldName,
    bool IsMatch,
    string Message);

public sealed record BackendSettingsComparisonResult(
    bool CanCompare,
    string SummaryMessage,
    IReadOnlyList<BackendFieldComparison> Fields);
