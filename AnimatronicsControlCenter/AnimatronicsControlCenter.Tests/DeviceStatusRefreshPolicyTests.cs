using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class DeviceStatusRefreshPolicyTests
{
    [TestMethod]
    public void ShouldRun_ReturnsTrue_WhenDeviceIsSelectedAndInitialLoadIsComplete()
    {
        bool shouldRun = DeviceStatusRefreshPolicy.ShouldRun(
            hasSelectedDevice: true,
            isInitialLoadInProgress: false,
            isPeriodicPingEnabled: true);

        Assert.IsTrue(shouldRun);
    }

    [TestMethod]
    public void ShouldRun_ReturnsFalse_DuringInitialLoad()
    {
        bool shouldRun = DeviceStatusRefreshPolicy.ShouldRun(
            hasSelectedDevice: true,
            isInitialLoadInProgress: true,
            isPeriodicPingEnabled: true);

        Assert.IsFalse(shouldRun);
    }

    [TestMethod]
    public void ShouldRun_ReturnsFalse_WhenNoDeviceIsSelected()
    {
        bool shouldRun = DeviceStatusRefreshPolicy.ShouldRun(
            hasSelectedDevice: false,
            isInitialLoadInProgress: false,
            isPeriodicPingEnabled: true);

        Assert.IsFalse(shouldRun);
    }

    [TestMethod]
    public void ShouldRun_ReturnsFalse_WhenPeriodicPingIsDisabled()
    {
        bool shouldRun = DeviceStatusRefreshPolicy.ShouldRun(
            hasSelectedDevice: true,
            isInitialLoadInProgress: false,
            isPeriodicPingEnabled: false);

        Assert.IsFalse(shouldRun);
    }

    [TestMethod]
    public void GetIntervalMs_ConvertsSecondsToMilliseconds()
    {
        Assert.AreEqual(5000, DeviceStatusRefreshPolicy.GetIntervalMs(5));
    }

    [TestMethod]
    public void GetIntervalMs_ClampsBelowOneSecond()
    {
        Assert.AreEqual(1000, DeviceStatusRefreshPolicy.GetIntervalMs(0));
    }
}
