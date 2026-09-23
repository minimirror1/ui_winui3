using AnimatronicsControlCenter.Core.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class PingTimeSettingsTests
{
    [TestMethod]
    public void TimeZones_IncludeMultipleUsOffsets()
    {
        var usOptions = PingTimeZoneCatalog.GetOptionsForCountry("US");

        Assert.IsTrue(usOptions.Count >= 4);
        Assert.IsTrue(usOptions.Any(option => option.UtcOffsetMinutes == -300));
        Assert.IsTrue(usOptions.Any(option => option.UtcOffsetMinutes == -480));
    }

    [TestMethod]
    public void CreatePayload_UsesSelectedOffsetAndUppercaseCountry()
    {
        var utcNow = new DateTimeOffset(2026, 4, 24, 6, 30, 45, TimeSpan.Zero);

        PingTimePayload payload = PingTimePayloadFactory.Create("kr", 540, utcNow);

        Assert.AreEqual("KR", payload.CountryCode);
        Assert.AreEqual(new DateTimeOffset(2026, 4, 24, 15, 30, 45, TimeSpan.FromHours(9)), payload.Timestamp);
    }

    [TestMethod]
    public void FormatPreview_IncludesCountryTimestampAndOffset()
    {
        var utcNow = new DateTimeOffset(2026, 4, 24, 6, 30, 45, TimeSpan.Zero);

        string preview = PingTimePayloadFactory.FormatPreview("KR", 540, utcNow);

        StringAssert.Contains(preview, "KR");
        StringAssert.Contains(preview, "2026-04-24 15:30:45");
        StringAssert.Contains(preview, "UTC+09:00");
    }

    // ── 타임존 ID 기반 DST 자동 적용 ─────────────────────────────

    [TestMethod]
    public void Options_AllHaveResolvableTimeZoneIds()
    {
        foreach (var option in PingTimeZoneCatalog.All)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(option.TimeZoneId);
            Assert.IsNotNull(tz, $"Unresolvable timezone id: {option.TimeZoneId}");
        }
    }

    [TestMethod]
    public void GetUtcOffsetMinutes_UsEastern_AppliesDaylightSavingTime()
    {
        var option = PingTimeZoneCatalog.All.First(o => o.TimeZoneId == "America/New_York");

        int summer = option.GetUtcOffsetMinutes(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));
        int winter = option.GetUtcOffsetMinutes(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(-240, summer, "US Eastern in July must be UTC-04:00 (DST).");
        Assert.AreEqual(-300, winter, "US Eastern in January must be UTC-05:00 (standard).");
    }

    [TestMethod]
    public void GetUtcOffsetMinutes_Korea_HasNoDaylightSavingTime()
    {
        var option = PingTimeZoneCatalog.All.First(o => o.TimeZoneId == "Asia/Seoul");

        int summer = option.GetUtcOffsetMinutes(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));
        int winter = option.GetUtcOffsetMinutes(new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero));

        Assert.AreEqual(540, summer);
        Assert.AreEqual(540, winter);
    }

    [TestMethod]
    public void CreatePayloadWithTimeZoneId_AppliesDstOffsetAtGivenInstant()
    {
        var summerUtc = new DateTimeOffset(2026, 7, 15, 6, 30, 45, TimeSpan.Zero);

        PingTimePayload payload = PingTimePayloadFactory.Create("us", "America/New_York", summerUtc);

        Assert.AreEqual("US", payload.CountryCode);
        Assert.AreEqual(
            new DateTimeOffset(2026, 7, 15, 2, 30, 45, TimeSpan.FromHours(-4)),
            payload.Timestamp);
    }

    [TestMethod]
    public void CreatePayloadWithTimeZoneId_UnknownId_FallsBackToSeoul()
    {
        var utcNow = new DateTimeOffset(2026, 4, 24, 6, 30, 45, TimeSpan.Zero);

        PingTimePayload payload = PingTimePayloadFactory.Create("KR", "Not/AZone", utcNow);

        Assert.AreEqual(TimeSpan.FromHours(9), payload.Timestamp.Offset);
    }

    [TestMethod]
    public void FindByLegacyOffset_MapsOldSettingsToTimeZoneId()
    {
        var eastern = PingTimeZoneCatalog.FindByLegacyOffsetOrDefault("US", -300);
        var pacific = PingTimeZoneCatalog.FindByLegacyOffsetOrDefault("US", -480);
        var unknown = PingTimeZoneCatalog.FindByLegacyOffsetOrDefault("ZZ", 123);

        Assert.AreEqual("America/New_York", eastern.TimeZoneId);
        Assert.AreEqual("America/Los_Angeles", pacific.TimeZoneId);
        Assert.AreEqual("Asia/Seoul", unknown.TimeZoneId, "Unknown legacy settings fall back to KR.");
    }

    [TestMethod]
    public void FindByTimeZoneIdOrDefault_ReturnsMatchingOptionOrSeoul()
    {
        Assert.AreEqual("America/Chicago", PingTimeZoneCatalog.FindByTimeZoneIdOrDefault("America/Chicago").TimeZoneId);
        Assert.AreEqual("Asia/Seoul", PingTimeZoneCatalog.FindByTimeZoneIdOrDefault("Not/AZone").TimeZoneId);
        Assert.AreEqual("Asia/Seoul", PingTimeZoneCatalog.FindByTimeZoneIdOrDefault(null).TimeZoneId);
    }
}
