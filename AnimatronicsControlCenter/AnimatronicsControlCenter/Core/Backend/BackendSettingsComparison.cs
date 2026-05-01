using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimatronicsControlCenter.Core.Backend;

public static class BackendSettingsComparison
{
    public static BackendSettingsComparisonResult Compare(
        BackendServerSnapshot? server,
        BackendLocalSettingsSnapshot local)
    {
        if (server is null)
        {
            return new BackendSettingsComparisonResult(
                CanCompare: false,
                SummaryMessage: "서버 값을 먼저 조회해야 비교할 수 있습니다.",
                Fields: Array.Empty<BackendFieldComparison>());
        }

        var fields = new List<BackendFieldComparison>
        {
            CompareText("StoreId", "Store ID", server.StoreId, local.StoreId),
            CompareText("StoreName", "Store Name", server.StoreName, local.StoreName),
            CompareText("StoreCountryCode", "Store Country Code", server.StoreCountryCode, local.StoreCountryCode),
            CompareText("PcId", "PC ID", server.PcId, local.PcId),
            CompareText("PcName", "PC Name", server.PcName, local.PcName),
            CompareText("SwVersion", "sw_version", server.SwVersion, local.SwVersion),
            CompareObjectMappings(server.Objects, local.DeviceObjectMappings)
        };

        return new BackendSettingsComparisonResult(
            CanCompare: true,
            SummaryMessage: fields.All(field => field.IsMatch) ? "서버 값과 로컬 설정이 일치합니다." : "서버 값과 다른 로컬 설정이 있습니다.",
            Fields: fields);
    }

    private static BackendFieldComparison CompareText(string fieldName, string displayName, string? serverValue, string? localValue)
    {
        string normalizedServer = serverValue ?? string.Empty;
        string normalizedLocal = localValue ?? string.Empty;
        if (string.Equals(normalizedServer, normalizedLocal, StringComparison.Ordinal))
        {
            return new BackendFieldComparison(fieldName, true, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(normalizedLocal))
        {
            return new BackendFieldComparison(fieldName, false, $"로컬 설정값이 비어 있습니다. 서버 값은 '{normalizedServer}'입니다.");
        }

        if (string.IsNullOrWhiteSpace(normalizedServer))
        {
            return new BackendFieldComparison(fieldName, false, "서버 조회 결과에 이 값이 없습니다. Store/PC/Object ID를 확인하세요.");
        }

        return new BackendFieldComparison(fieldName, false, $"{displayName}: 서버 값 '{normalizedServer}'와 로컬 값 '{normalizedLocal}'이 다릅니다.");
    }

    private static BackendFieldComparison CompareObjectMappings(
        IReadOnlyList<BackendServerObjectSnapshot> serverObjects,
        IReadOnlyDictionary<int, string> localMappings)
    {
        if (localMappings.Count == 0)
        {
            return new BackendFieldComparison("DeviceObjectMappings", false, "로컬 장치에 대응하는 서버 Object ID가 없습니다.");
        }

        var serverIds = serverObjects.Select(obj => obj.ObjectId).ToHashSet(StringComparer.Ordinal);
        foreach (var mapping in localMappings.OrderBy(item => item.Key))
        {
            if (string.IsNullOrWhiteSpace(mapping.Value))
            {
                return new BackendFieldComparison("DeviceObjectMappings", false, $"로컬 장치 {mapping.Key}에 대응하는 서버 Object ID가 없습니다.");
            }

            if (!serverIds.Contains(mapping.Value))
            {
                return new BackendFieldComparison("DeviceObjectMappings", false, $"서버의 선택 PC 아래에 Object ID '{mapping.Value}'가 없습니다.");
            }
        }

        return new BackendFieldComparison("DeviceObjectMappings", true, string.Empty);
    }
}
