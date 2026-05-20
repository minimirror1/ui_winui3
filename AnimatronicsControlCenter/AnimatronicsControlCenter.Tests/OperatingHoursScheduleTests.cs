using System.Text.Json;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.OperatingHours;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class OperatingHoursScheduleTests
{
    private const string StoreDetailJson = """
    {
      "id": "0PK85FSNX2TFA",
      "store_name": "Test Store",
      "country_code": "KR",
      "address": "Seoul",
      "latitude": 37.545424,
      "longitude": 127.224058,
      "timezone": "Asia/Seoul",
      "operate_times": [
        { "day_of_week": "FRI", "open_time": "09:00", "close_time": "18:00" },
        { "day_of_week": "MON", "open_time": "00:00", "close_time": "00:00" },
        { "day_of_week": "SAT", "open_time": "10:00", "close_time": "19:00" },
        { "day_of_week": "SUN", "open_time": "10:00", "close_time": "19:00" },
        { "day_of_week": "THU", "open_time": "09:00", "close_time": "18:00" },
        { "day_of_week": "TUE", "open_time": "09:30", "close_time": "18:30" },
        { "day_of_week": "WED", "open_time": "09:00", "close_time": "18:00" }
      ],
      "created_at": "2026-02-25T01:49:43.727Z",
      "modified_at": "2026-02-26T01:49:43.727Z"
    }
    """;

    [TestMethod]
    public void StoreDetailResponse_ReadsActualStoreDetailFields()
    {
        var response = JsonSerializer.Deserialize<BackendStoreDetailResponse>(StoreDetailJson);

        Assert.IsNotNull(response);
        Assert.AreEqual("0PK85FSNX2TFA", response.StoreId);
        Assert.AreEqual("Asia/Seoul", response.Timezone);
        Assert.AreEqual("2026-02-26T01:49:43.727Z", response.ModifiedAt);
        Assert.AreEqual(7, response.OperateTimes!.Count);
    }

    [TestMethod]
    public void FromStoreDetail_ReturnsSevenDaysInMondayToSundayOrder()
    {
        var response = JsonSerializer.Deserialize<BackendStoreDetailResponse>(StoreDetailJson)!;

        var schedule = OperatingHoursSchedule.FromStoreDetail(response);

        CollectionAssert.AreEqual(
            new[] { "MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN" },
            schedule.Days.Select(day => day.DayOfWeek).ToArray());
        Assert.AreEqual(7, schedule.Days.Count);
        Assert.AreEqual("0PK85FSNX2TFA", schedule.StoreId);
        Assert.AreEqual("Asia/Seoul", schedule.Timezone);
        Assert.AreEqual("2026-02-26T01:49:43.727Z", schedule.ModifiedAt);
    }

    [TestMethod]
    public void FromStoreDetail_TreatsMidnightToMidnightAsClosed()
    {
        var response = JsonSerializer.Deserialize<BackendStoreDetailResponse>(StoreDetailJson)!;

        var schedule = OperatingHoursSchedule.FromStoreDetail(response);

        var monday = schedule.Days[0];
        Assert.IsTrue(monday.IsClosed);
        Assert.AreEqual(0, monday.OpenMinutes);
        Assert.AreEqual(0, monday.CloseMinutes);
    }

    [TestMethod]
    public void FromStoreDetail_ProducesStableChecksumForSameData()
    {
        var firstResponse = JsonSerializer.Deserialize<BackendStoreDetailResponse>(StoreDetailJson)!;
        var secondResponse = JsonSerializer.Deserialize<BackendStoreDetailResponse>(StoreDetailJson)!;

        var first = OperatingHoursSchedule.FromStoreDetail(firstResponse);
        var second = OperatingHoursSchedule.FromStoreDetail(secondResponse);

        Assert.AreNotEqual(0u, first.Checksum);
        Assert.AreEqual(first.Checksum, second.Checksum);
    }
}
