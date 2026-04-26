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
}
