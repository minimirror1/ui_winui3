using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimatronicsControlCenter.Core.Protocol;

public sealed record PingTimeZoneOption(string CountryCode, string DisplayName, int UtcOffsetMinutes)
{
    public string OffsetText => PingTimePayloadFactory.FormatOffset(UtcOffsetMinutes);

    public string Label => $"{CountryCode.ToUpperInvariant()} {DisplayName} {OffsetText}";
}

public static class PingTimeZoneCatalog
{
    private static readonly IReadOnlyList<PingTimeZoneOption> Options =
    [
        new("KR", "Korea", 540),
        new("US", "Eastern", -300),
        new("US", "Central", -360),
        new("US", "Mountain", -420),
        new("US", "Pacific", -480),
        new("JP", "Japan", 540),
        new("CN", "China", 480),
        new("GB", "United Kingdom", 0),
        new("DE", "Germany", 60),
        new("FR", "France", 60),
    ];

    public static IReadOnlyList<PingTimeZoneOption> All => Options;

    public static IReadOnlyList<PingTimeZoneOption> GetOptionsForCountry(string countryCode)
    {
        var normalized = NormalizeCountryCodeOrDefault(countryCode);
        return Options
            .Where(option => option.CountryCode.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static PingTimeZoneOption FindOrDefault(string countryCode, int utcOffsetMinutes)
    {
        var normalized = NormalizeCountryCodeOrDefault(countryCode);
        return Options.FirstOrDefault(option =>
                option.CountryCode.Equals(normalized, StringComparison.OrdinalIgnoreCase) &&
                option.UtcOffsetMinutes == utcOffsetMinutes)
            ?? Options.First(option => option.CountryCode == "KR");
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
    public static PingTimePayload Create(string countryCode, int utcOffsetMinutes, DateTimeOffset utcNow)
    {
        string normalizedCountryCode = PingTimeZoneCatalog.NormalizeCountryCodeOrDefault(countryCode);
        var timestamp = utcNow.ToUniversalTime().ToOffset(TimeSpan.FromMinutes(utcOffsetMinutes));
        return new PingTimePayload(normalizedCountryCode, timestamp);
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
