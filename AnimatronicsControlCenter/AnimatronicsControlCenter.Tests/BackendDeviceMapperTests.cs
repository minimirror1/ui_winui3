using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendDeviceMapperTests
{
    [DataTestMethod]
    [DataRow(MotionState.Playing, "PLAY")]
    [DataRow(MotionState.Stopped, "STOP")]
    [DataRow(MotionState.Idle, "STOP")]
    [DataRow(MotionState.Paused, "STOP")]
    public void CreateObjectLog_MapsMotionState(MotionState state, string expected)
    {
        var device = new Device(2) { MotionState = state, PowerStatus = "ON" };

        BackendObjectLogRequest log = BackendDeviceMapper.CreateObjectLog(device);

        Assert.AreEqual(expected, log.OperationStatus);
    }

    [DataTestMethod]
    [DataRow("ON", "ON")]
    [DataRow("OFF", "OFF")]
    [DataRow("", "OFF")]
    [DataRow("UNKNOWN", "OFF")]
    public void CreateObjectLog_UsesFirmwarePowerStatus(string powerStatus, string expected)
    {
        var device = new Device(2) { PowerStatus = powerStatus };

        BackendObjectLogRequest log = BackendDeviceMapper.CreateObjectLog(device);

        Assert.AreEqual(expected, log.PowerStatus);
    }

    [TestMethod]
    public void CreateObjectLog_MapsMotorErrorsToErrorData()
    {
        var device = new Device(2) { PowerStatus = "ON", MotionState = MotionState.Stopped };
        device.Motors.Add(new MotorState
        {
            Id = 2,
            GroupId = 1,
            SubId = 2,
            Type = "AC",
            Status = "Error"
        });

        BackendObjectLogRequest log = BackendDeviceMapper.CreateObjectLog(device);

        Assert.AreEqual("ON", log.PowerStatus);
        Assert.AreEqual("STOP", log.OperationStatus);
        Assert.AreEqual(1, log.ErrorData.Count);
        Assert.AreEqual("1-2", log.ErrorData[0].BoardId);
        Assert.AreEqual("AC", log.ErrorData[0].BoardType);
        Assert.AreEqual("Error", log.ErrorData[0].ErrorCode);
    }
}
