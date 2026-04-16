using System.Linq;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class ComRawTrafficTapTests
{
    [TestMethod]
    public void RecordBytes_DoesNothingWhileCaptureIsDisabled()
    {
        var tap = new ComRawTrafficTap();

        tap.RecordTxBytes(new byte[] { 0x7E, 0x00, 0x10 });
        tap.RecordRxBytes(new byte[] { 0x7E, 0x90, 0x01 });

        Assert.AreEqual(0, tap.GetSnapshot().Count);
    }

    [TestMethod]
    public void RecordBytes_StoresTimestampedHexLinesWhenCaptureIsEnabled()
    {
        var tap = new ComRawTrafficTap
        {
            IsCaptureEnabled = true
        };

        tap.RecordTxBytes(new byte[] { 0x7E, 0x00, 0x10 });
        tap.RecordRxBytes(new byte[] { 0x7E, 0x90, 0x01 });

        var entries = tap.GetSnapshot().ToArray();

        Assert.AreEqual(2, entries.Length);
        Assert.AreEqual(SerialTrafficDirection.Tx, entries[0].Direction);
        Assert.AreEqual("7E 00 10", entries[0].Line);
        Assert.AreEqual(SerialTrafficDirection.Rx, entries[1].Direction);
        Assert.AreEqual("7E 90 01", entries[1].Line);
    }
}
