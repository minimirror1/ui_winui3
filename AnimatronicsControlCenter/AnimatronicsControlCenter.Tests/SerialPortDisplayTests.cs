using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class SerialPortDisplayTests
{
    [TestMethod]
    public void CreateOption_MarksDigiXBeeDeviceAsLikelyXBee()
    {
        var info = new SerialPortDeviceInfo(
            PortName: "COM7",
            FriendlyName: "Digi XBee USB Interface (COM7)",
            Manufacturer: "Digi International",
            DeviceDescription: "USB Serial Port",
            HardwareId: "USB\\VID_0403&PID_6015");

        var option = SerialPortDisplay.CreateOption("COM7", info);

        Assert.AreEqual("COM7", option.PortName);
        Assert.IsTrue(option.IsLikelyXBee);
        StringAssert.Contains(option.DisplayName, "COM7");
        StringAssert.Contains(option.DisplayName, "XBee");
        StringAssert.Contains(option.DisplayName, "Digi XBee USB Interface");
    }

    [TestMethod]
    public void CreateOption_ShowsUsbDeviceNameForNonXBeePorts()
    {
        var info = new SerialPortDeviceInfo(
            PortName: "COM4",
            FriendlyName: "USB Serial Device (COM4)",
            Manufacturer: "Microsoft",
            DeviceDescription: "USB Serial Device",
            HardwareId: "USB\\VID_1234&PID_5678");

        var option = SerialPortDisplay.CreateOption("COM4", info);

        Assert.IsFalse(option.IsLikelyXBee);
        Assert.AreEqual("COM4 (USB Serial Device)", option.DisplayName);
    }

    [TestMethod]
    public void CreateOption_FallsBackToPortNameWithoutUsbMetadata()
    {
        var option = SerialPortDisplay.CreateOption("COM9", null);

        Assert.AreEqual("COM9", option.PortName);
        Assert.AreEqual("COM9", option.DisplayName);
        Assert.IsFalse(option.IsLikelyXBee);
    }
}
