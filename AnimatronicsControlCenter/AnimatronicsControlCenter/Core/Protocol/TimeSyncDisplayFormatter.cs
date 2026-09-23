using System;

namespace AnimatronicsControlCenter.Core.Protocol;

/// UI에 표시할 시간 문자열 조립 — 국가(카탈로그 라벨) + DST 반영 오프셋 + 서머타임 적용 여부 포함.
public static class TimeSyncDisplayFormatter
{
    /// 설정 카드용: "2026-07-15 02:30:45 (US Eastern, UTC-04:00 · 서머타임 적용 중)"
    public static string FormatCurrentTime(string? timeZoneId, DateTimeOffset utcNow, string dstActiveLabel)
        => Format(timeZoneId, utcNow, dstActiveLabel, includeDate: true);

    /// 대시보드 배지용(날짜 생략): "US Eastern 02:30:45 (UTC-04:00 · 서머타임 적용 중)"
    public static string FormatBadgeTime(string? timeZoneId, DateTimeOffset utcNow, string dstActiveLabel)
        => Format(timeZoneId, utcNow, dstActiveLabel, includeDate: false);

    private static string Format(string? timeZoneId, DateTimeOffset utcNow, string dstActiveLabel, bool includeDate)
    {
        var option = PingTimeZoneCatalog.FindByTimeZoneIdOrDefault(timeZoneId);
        var zone = PingTimeZoneCatalog.ResolveTimeZone(option.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(utcNow, zone);

        string country = $"{option.CountryCode} {option.DisplayName}";
        string offset = PingTimePayloadFactory.FormatOffset((int)Math.Round(localNow.Offset.TotalMinutes));
        string dstSuffix = zone.IsDaylightSavingTime(utcNow) ? $" · {dstActiveLabel}" : string.Empty;

        return includeDate
            ? $"{localNow:yyyy-MM-dd HH:mm:ss} ({country}, {offset}{dstSuffix})"
            : $"{country} {localNow:HH:mm:ss} ({offset}{dstSuffix})";
    }
}
