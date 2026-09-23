using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimatronicsControlCenter.Core.Protocol;

/// IANA 타임존 ID 기반 옵션 — 오프셋은 호출 시점 기준으로 계산되어 서머타임(DST)이 자동 반영된다.
public sealed record PingTimeZoneOption(string CountryCode, string DisplayName, string TimeZoneId)
{
    /// 표준시(비 DST) 오프셋 분. 구 설정(고정 오프셋) 매칭·호환용.
    public int UtcOffsetMinutes
        => (int)Math.Round(PingTimeZoneCatalog.ResolveTimeZone(TimeZoneId).BaseUtcOffset.TotalMinutes);

    /// utcNow 시점의 실제 오프셋 분 (DST 반영).
    public int GetUtcOffsetMinutes(DateTimeOffset utcNow)
        => (int)Math.Round(PingTimeZoneCatalog.ResolveTimeZone(TimeZoneId).GetUtcOffset(utcNow).TotalMinutes);

    public string OffsetText => PingTimePayloadFactory.FormatOffset(GetUtcOffsetMinutes(DateTimeOffset.UtcNow));

    public string Label => $"{CountryCode.ToUpperInvariant()} {DisplayName} {OffsetText}";
}

public static class PingTimeZoneCatalog
{
    public const string DefaultTimeZoneId = "Asia/Seoul";

    private static readonly IReadOnlyList<PingTimeZoneOption> Options =
    [
        new("KR", "Korea", "Asia/Seoul"),
        new("US", "Eastern", "America/New_York"),
        new("US", "Central", "America/Chicago"),
        new("US", "Mountain", "America/Denver"),
        new("US", "Pacific", "America/Los_Angeles"),
        new("JP", "Japan", "Asia/Tokyo"),
        new("CN", "China", "Asia/Shanghai"),
        new("GB", "United Kingdom", "Europe/London"),
        new("DE", "Germany", "Europe/Berlin"),
        new("FR", "France", "Europe/Paris"),
    ];

    public static IReadOnlyList<PingTimeZoneOption> All => Options;

    /// 타임존 ID 해석. 알 수 없는 ID는 서울로 폴백.
    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId) &&
            TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone))
        {
            return zone;
        }

        return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimeZoneId);
    }

    public static IReadOnlyList<PingTimeZoneOption> GetOptionsForCountry(string countryCode)
    {
        var normalized = NormalizeCountryCodeOrDefault(countryCode);
        return Options
            .Where(option => option.CountryCode.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// 구 설정(국가코드 + 고정 오프셋 분)을 타임존 옵션으로 마이그레이션. 매칭 실패 시 KR.
    public static PingTimeZoneOption FindByLegacyOffsetOrDefault(string countryCode, int utcOffsetMinutes)
    {
        var normalized = NormalizeCountryCodeOrDefault(countryCode);
        return Options.FirstOrDefault(option =>
                option.CountryCode.Equals(normalized, StringComparison.OrdinalIgnoreCase) &&
                option.UtcOffsetMinutes == utcOffsetMinutes)
            ?? Options.First(option => option.TimeZoneId == DefaultTimeZoneId);
    }

    public static PingTimeZoneOption FindByTimeZoneIdOrDefault(string? timeZoneId)
    {
        return Options.FirstOrDefault(option =>
                option.TimeZoneId.Equals(timeZoneId, StringComparison.OrdinalIgnoreCase))
            ?? Options.First(option => option.TimeZoneId == DefaultTimeZoneId);
    }

    public static string NormalizeCountryCodeOrDefault(string countryCode)
    {
        if (countryCode.Length == 2 &&
            char.IsAsciiLetter(countryCode[0]) &&
            char.IsAsciiLetter(countryCode[1]))
        {
            return countryCode.ToUpperInvariant();
        }

        return "KR";
    }
}

public static class PingTimePayloadFactory
{
    /// 타임존 ID 기반 — utcNow 시점의 DST 반영 오프셋으로 로컬 시각을 만든다.
    public static PingTimePayload Create(string countryCode, string timeZoneId, DateTimeOffset utcNow)
    {
        var zone = PingTimeZoneCatalog.ResolveTimeZone(timeZoneId);
        int offsetMinutes = (int)Math.Round(zone.GetUtcOffset(utcNow).TotalMinutes);
        return Create(countryCode, offsetMinutes, utcNow);
    }

    public static PingTimePayload Create(string countryCode, int utcOffsetMinutes, DateTimeOffset utcNow)
    {
        string normalizedCountryCode = PingTimeZoneCatalog.NormalizeCountryCodeOrDefault(countryCode);
        var timestamp = utcNow.ToUniversalTime().ToOffset(TimeSpan.FromMinutes(utcOffsetMinutes));
        return new PingTimePayload(normalizedCountryCode, timestamp);
    }

    public static string FormatPreview(string countryCode, string timeZoneId, DateTimeOffset utcNow)
    {
        var zone = PingTimeZoneCatalog.ResolveTimeZone(timeZoneId);
        int offsetMinutes = (int)Math.Round(zone.GetUtcOffset(utcNow).TotalMinutes);
        return FormatPreview(countryCode, offsetMinutes, utcNow);
    }

    public static string FormatPreview(string countryCode, int utcOffsetMinutes, DateTimeOffset utcNow)
    {
        var payload = Create(countryCode, utcOffsetMinutes, utcNow);
        return $"{payload.CountryCode} {payload.Timestamp:yyyy-MM-dd HH:mm:ss} {FormatOffset(utcOffsetMinutes)}";
    }

    public static string FormatOffset(int utcOffsetMinutes)
    {
        char sign = utcOffsetMinutes < 0 ? '-' : '+';
        int absoluteMinutes = Math.Abs(utcOffsetMinutes);
        return $"UTC{sign}{absoluteMinutes / 60:D2}:{absoluteMinutes % 60:D2}";
    }
}
