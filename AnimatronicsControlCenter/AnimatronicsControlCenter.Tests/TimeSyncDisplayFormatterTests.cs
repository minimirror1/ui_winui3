using System;
using AnimatronicsControlCenter.Core.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class TimeSyncDisplayFormatterTests
{
    private const string DstLabel = "서머타임 적용 중";

    [TestMethod]
    public void FormatCurrentTime_UsEasternSummer_IncludesCountryOffsetAndDst()
    {
        var summerUtc = new DateTimeOffset(2026, 7, 15, 6, 30, 45, TimeSpan.Zero);

        string text = TimeSyncDisplayFormatter.FormatCurrentTime("America/New_York", summerUtc, DstLabel);

        StringAssert.Contains(text, "2026-07-15 02:30:45", "여름 US Eastern은 UTC-4 로컬 시각이어야 한다.");
        StringAssert.Contains(text, "US Eastern");
        StringAssert.Contains(text, "UTC-04:00");
        StringAssert.Contains(text, DstLabel);
    }

    [TestMethod]
    public void FormatCurrentTime_KoreaWinter_IncludesCountryButNoDst()
    {
        var winterUtc = new DateTimeOffset(2026, 1, 15, 6, 30, 45, TimeSpan.Zero);

        string text = TimeSyncDisplayFormatter.FormatCurrentTime("Asia/Seoul", winterUtc, DstLabel);

        StringAssert.Contains(text, "2026-01-15 15:30:45");
        StringAssert.Contains(text, "KR Korea");
        StringAssert.Contains(text, "UTC+09:00");
        Assert.IsFalse(text.Contains(DstLabel), "KR은 서머타임이 없으므로 표시하면 안 된다.");
    }

    [TestMethod]
    public void FormatCurrentTime_UsEasternWinter_NoDstLabel()
    {
        var winterUtc = new DateTimeOffset(2026, 1, 15, 6, 30, 45, TimeSpan.Zero);

        string text = TimeSyncDisplayFormatter.FormatCurrentTime("America/New_York", winterUtc, DstLabel);

        StringAssert.Contains(text, "2026-01-15 01:30:45");
        StringAssert.Contains(text, "UTC-05:00");
        Assert.IsFalse(text.Contains(DstLabel));
    }

    [TestMethod]
    public void FormatCurrentTime_UnknownTimeZoneId_FallsBackToSeoul()
    {
        var utcNow = new DateTimeOffset(2026, 9, 4, 3, 0, 0, TimeSpan.Zero);

        string text = TimeSyncDisplayFormatter.FormatCurrentTime("Not/AZone", utcNow, DstLabel);

        StringAssert.Contains(text, "KR Korea");
        StringAssert.Contains(text, "UTC+09:00");
    }

    [TestMethod]
    public void FormatBadgeTime_IncludesCountryTimeOffsetAndDst_WithoutDate()
    {
        var summerUtc = new DateTimeOffset(2026, 7, 15, 6, 30, 45, TimeSpan.Zero);

        string text = TimeSyncDisplayFormatter.FormatBadgeTime("America/New_York", summerUtc, DstLabel);

        StringAssert.Contains(text, "US Eastern");
        StringAssert.Contains(text, "02:30:45");
        StringAssert.Contains(text, "UTC-04:00");
        StringAssert.Contains(text, DstLabel);
        Assert.IsFalse(text.Contains("2026"), "배지에는 날짜를 넣지 않는다.");
    }
}
