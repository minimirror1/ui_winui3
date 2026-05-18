using System;
using System.Linq;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendTrafficStateTests
{
    [TestMethod]
    public void GetSnapshot_MarksUplinkAndDownlinkActive_WithinActivityWindow()
    {
        var state = new BackendTrafficState();
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");

        state.RecordRequest(HttpMethod.Get, new Uri("https://server.test/v1/stores"), now);
        state.RecordResponse(HttpMethod.Get, new Uri("https://server.test/v1/stores"), 200, TimeSpan.FromMilliseconds(42), "OK", now.AddMilliseconds(20));

        var snapshot = state.GetSnapshot(now.AddMilliseconds(250));

        Assert.IsTrue(snapshot.IsUplinkActive);
        Assert.IsTrue(snapshot.IsDownlinkActive);
    }

    [TestMethod]
    public void GetSnapshot_ExpiresActivity_AfterActivityWindow()
    {
        var state = new BackendTrafficState();
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");

        state.RecordRequest(HttpMethod.Post, new Uri("https://server.test/v1/logs"), now);
        state.RecordResponse(HttpMethod.Post, new Uri("https://server.test/v1/logs"), 200, TimeSpan.FromMilliseconds(20), "OK", now);

        var snapshot = state.GetSnapshot(now.AddMilliseconds(351));

        Assert.IsFalse(snapshot.IsUplinkActive);
        Assert.IsFalse(snapshot.IsDownlinkActive);
    }

    [TestMethod]
    public void GetSnapshot_ReturnsOnline_WhenSuccessfulCommunicationIsRecent()
    {
        var state = new BackendTrafficState();
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");

        state.RecordResponse(HttpMethod.Get, new Uri("https://server.test/v1/stores"), 204, TimeSpan.FromMilliseconds(12), "OK", now);

        var snapshot = state.GetSnapshot(now.AddSeconds(30));

        Assert.IsTrue(snapshot.IsServerOnline);
        Assert.AreEqual(now, snapshot.LastSuccessAt);
        Assert.IsNull(snapshot.LastFailureAt);
    }

    [TestMethod]
    public void GetSnapshot_ReturnsOffline_WhenSuccessfulCommunicationIsStale()
    {
        var state = new BackendTrafficState();
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");

        state.RecordResponse(HttpMethod.Get, new Uri("https://server.test/v1/stores"), 200, TimeSpan.FromMilliseconds(12), "OK", now);

        var snapshot = state.GetSnapshot(now.AddMinutes(6));

        Assert.IsFalse(snapshot.IsServerOnline);
    }

    [TestMethod]
    public void GetEntries_KeepsLatestOneHundredRecords()
    {
        var state = new BackendTrafficState();
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");

        for (int i = 0; i < 105; i++)
        {
            state.RecordResponse(HttpMethod.Get, new Uri($"https://server.test/v1/items/{i}"), 200, TimeSpan.FromMilliseconds(i), "OK", now.AddSeconds(i));
        }

        var entries = state.GetEntries().ToList();

        Assert.AreEqual(100, entries.Count);
        Assert.AreEqual("/v1/items/5", entries.First().Path);
        Assert.AreEqual("/v1/items/104", entries.Last().Path);
    }

    [TestMethod]
    public void RecordTraffic_UpdatesRequestResponseErrorAndTotalCounts()
    {
        var state = new BackendTrafficState();
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");

        state.RecordRequest(HttpMethod.Get, new Uri("https://server.test/v1/stores"), now);
        state.RecordResponse(HttpMethod.Get, new Uri("https://server.test/v1/stores"), 200, TimeSpan.FromMilliseconds(42), "OK", now);
        state.RecordError(HttpMethod.Get, new Uri("https://server.test/v1/stores"), TimeSpan.Zero, "Failed", now);

        var counts = state.GetCounts();

        Assert.AreEqual(1, counts.RequestCount);
        Assert.AreEqual(1, counts.ResponseCount);
        Assert.AreEqual(1, counts.ErrorCount);
        Assert.AreEqual(3, counts.TotalCount);
    }

    [TestMethod]
    public void ClearCounts_DoesNotClearTrafficEntries()
    {
        var state = new BackendTrafficState();
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");
        state.RecordRequest(HttpMethod.Get, new Uri("https://server.test/v1/stores"), now);
        state.RecordResponse(HttpMethod.Get, new Uri("https://server.test/v1/stores"), 200, TimeSpan.FromMilliseconds(42), "OK", now);

        state.ClearCounts();

        var counts = state.GetCounts();
        Assert.AreEqual(0, counts.RequestCount);
        Assert.AreEqual(0, counts.ResponseCount);
        Assert.AreEqual(0, counts.ErrorCount);
        Assert.AreEqual(0, counts.TotalCount);
        Assert.AreEqual(2, state.GetEntries().Count);
    }
}
