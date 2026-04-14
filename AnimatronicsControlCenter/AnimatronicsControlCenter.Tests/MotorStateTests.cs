using AnimatronicsControlCenter.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class MotorStateTests
{
    [TestMethod]
    public void AnglePosition_MapsRawPositionIntoConfiguredAngleRange()
    {
        var motor = new MotorState
        {
            MinAngle = 0,
            MaxAngle = 180,
            MinRaw = 0,
            MaxRaw = 3072,
            Position = 2048
        };

        Assert.AreEqual(120.0, motor.AnglePosition, 0.1);
    }

    [TestMethod]
    public void SettingAnglePosition_UpdatesRawPositionUsingConfiguredRanges()
    {
        var motor = new MotorState
        {
            MinAngle = 0,
            MaxAngle = 180,
            MinRaw = 0,
            MaxRaw = 3072
        };

        motor.AnglePosition = 120;

        Assert.AreEqual(2048, motor.Position, 0.1);
    }

    [TestMethod]
    public void PositionDisplay_ShowsAngleAndRawDataTogether()
    {
        var motor = new MotorState
        {
            MinAngle = 0,
            MaxAngle = 180,
            MinRaw = 0,
            MaxRaw = 3072,
            Position = 2048
        };

        Assert.AreEqual("120.0\u00B0(2048)", motor.PositionDisplay);
    }
}
