using System;
using System.Linq;
using System.Net.Http;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendTrafficTapTests
{
    [TestMethod]
    public void RecordRequest_RaisesTrafficChanged_AndMarksUplinkActive()
    {
        IBackendTrafficTap tap = new BackendTrafficTap();
        int changedCount = 0;
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");
        tap.TrafficChanged += (_, _) => changedCount++;

        tap.RecordRequest(HttpMethod.Get, new Uri("https://server.test/v1/stores"), now);

        var snapshot = tap.GetSnapshot(now);
        Assert.AreEqual(1, changedCount);
        Assert.IsTrue(snapshot.IsUplinkActive);
        Assert.AreEqual(1, tap.GetEntries().Count);
    }

    [TestMethod]
    public void RecordResponse_RaisesTrafficChanged_AndUpdatesLog()
    {
        IBackendTrafficTap tap = new BackendTrafficTap();
        int changedCount = 0;
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");
        tap.TrafficChanged += (_, _) => changedCount++;

        tap.RecordResponse(HttpMethod.Post, new Uri("https://server.test/v1/logs"), 201, TimeSpan.FromMilliseconds(35), "Created", now);

        var snapshot = tap.GetSnapshot(now);
        var entry = tap.GetEntries().Single();
        Assert.AreEqual(1, changedCount);
        Assert.IsTrue(snapshot.IsDownlinkActive);
        Assert.IsTrue(snapshot.IsServerOnline);
        Assert.AreEqual(201, entry.StatusCode);
        Assert.AreEqual("/v1/logs", entry.Path);
    }

    [TestMethod]
    public void ClearCounts_RaisesTrafficChanged_AndKeepsEntries()
    {
        IBackendTrafficTap tap = new BackendTrafficTap();
        int changedCount = 0;
        var now = DateTimeOffset.Parse("2026-05-11T10:00:00+09:00");
        tap.RecordRequest(HttpMethod.Get, new Uri("https://server.test/v1/stores"), now);
        tap.TrafficChanged += (_, _) => changedCount++;

        tap.ClearCounts();

        var counts = tap.GetCounts();
        Assert.AreEqual(1, changedCount);
        Assert.AreEqual(0, counts.TotalCount);
        Assert.AreEqual(1, tap.GetEntries().Count);
    }
}
