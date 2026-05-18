using System.Linq;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class SerialTrafficTapTests
{
    [TestMethod]
    public void RecordBytes_UpdatesTxRxAndTotalCounts()
    {
        var tap = new SerialTrafficTap();

        tap.RecordTxBytes(new byte[] { 0x00, 0x01 });
        tap.RecordRxBytes(new byte[] { 0x02 });
        tap.RecordTx("manual");

        var counts = tap.GetCounts();

        Assert.AreEqual(2, counts.TxCount);
        Assert.AreEqual(1, counts.RxCount);
        Assert.AreEqual(3, counts.TotalCount);
    }

    [TestMethod]
    public void ClearCounts_DoesNotClearRecordedEntries()
    {
        var tap = new SerialTrafficTap();
        tap.RecordTxBytes(new byte[] { 0x00, 0x01 });
        tap.RecordRxBytes(new byte[] { 0x02 });

        tap.ClearCounts();

        var counts = tap.GetCounts();
        Assert.AreEqual(0, counts.TxCount);
        Assert.AreEqual(0, counts.RxCount);
        Assert.AreEqual(0, counts.TotalCount);
        Assert.AreEqual(2, tap.GetSnapshot().Count);
    }
}
