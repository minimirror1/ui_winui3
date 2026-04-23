using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class DeviceStatusRefreshPolicyTests
{
    [TestMethod]
    public void ShouldRun_ReturnsTrue_WhenDeviceIsSelectedAndInitialLoadIsComplete()
    {
        bool shouldRun = DeviceStatusRefreshPolicy.ShouldRun(hasSelectedDevice: true, isInitialLoadInProgress: false);

        Assert.IsTrue(shouldRun);
    }

    [TestMethod]
    public void ShouldRun_ReturnsFalse_DuringInitialLoad()
    {
        bool shouldRun = DeviceStatusRefreshPolicy.ShouldRun(hasSelectedDevice: true, isInitialLoadInProgress: true);

        Assert.IsFalse(shouldRun);
    }

    [TestMethod]
    public void ShouldRun_ReturnsFalse_WhenNoDeviceIsSelected()
    {
        bool shouldRun = DeviceStatusRefreshPolicy.ShouldRun(hasSelectedDevice: false, isInitialLoadInProgress: false);

        Assert.IsFalse(shouldRun);
    }

    [TestMethod]
    public void IntervalMs_UsesLightweightOneSecondRefresh()
    {
        Assert.AreEqual(1000, DeviceStatusRefreshPolicy.IntervalMs);
    }
}
