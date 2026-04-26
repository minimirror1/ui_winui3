using System;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class SerialTrafficIndicatorStateTests
{
    [TestMethod]
    public void GetSnapshot_ReturnsInactive_WhenNoTrafficRecorded()
    {
        var state = new SerialTrafficIndicatorState();

        var snapshot = state.GetSnapshot(DateTimeOffset.Parse("2026-04-24T10:00:00+09:00"));

        Assert.IsFalse(snapshot.IsRxActive);
        Assert.IsFalse(snapshot.IsTxActive);
    }

    [TestMethod]
    public void Record_MarksRxActive_WithinActivityWindow()
    {
        var state = new SerialTrafficIndicatorState();
        var now = DateTimeOffset.Parse("2026-04-24T10:00:00+09:00");

        state.Record(new SerialTrafficEntry(now, SerialTrafficDirection.Rx, "AA"));
        var snapshot = state.GetSnapshot(now.AddMilliseconds(120));

        Assert.IsTrue(snapshot.IsRxActive);
        Assert.IsFalse(snapshot.IsTxActive);
    }

    [TestMethod]
    public void GetSnapshot_ExpiresActivity_AfterActivityWindow()
    {
        var state = new SerialTrafficIndicatorState();
        var now = DateTimeOffset.Parse("2026-04-24T10:00:00+09:00");

        state.Record(new SerialTrafficEntry(now, SerialTrafficDirection.Tx, "BB"));
        var snapshot = state.GetSnapshot(now.AddMilliseconds(351));

        Assert.IsFalse(snapshot.IsRxActive);
        Assert.IsFalse(snapshot.IsTxActive);
    }

    [TestMethod]
    public void GetSnapshot_ExpiresActivity_AfterResponsivePulseWindow()
    {
        var state = new SerialTrafficIndicatorState();
        var now = DateTimeOffset.Parse("2026-04-24T10:00:00+09:00");

        state.Record(new SerialTrafficEntry(now, SerialTrafficDirection.Rx, "AA"));
        var snapshot = state.GetSnapshot(now.AddMilliseconds(201));

        Assert.IsFalse(snapshot.IsRxActive);
    }

    [TestMethod]
    public void Record_TracksRxAndTx_Independently()
    {
        var state = new SerialTrafficIndicatorState();
        var baseTime = DateTimeOffset.Parse("2026-04-24T10:00:00+09:00");

        state.Record(new SerialTrafficEntry(baseTime, SerialTrafficDirection.Rx, "11"));
        state.Record(new SerialTrafficEntry(baseTime.AddMilliseconds(100), SerialTrafficDirection.Tx, "22"));

        var snapshot = state.GetSnapshot(baseTime.AddMilliseconds(220));

        Assert.IsFalse(snapshot.IsRxActive);
        Assert.IsTrue(snapshot.IsTxActive);
    }
}
