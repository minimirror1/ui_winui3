using System.Collections.ObjectModel;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Motors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class MotorStateMergerTests
{
    [TestMethod]
    public void Apply_PreservesMotorRangesWhenPartialStateOnlyUpdatesRawPosition()
    {
        var motors = new ObservableCollection<MotorState>
        {
            new()
            {
                Id = 1,
                MinAngle = 0,
                MaxAngle = 180,
                MinRaw = 0,
                MaxRaw = 3072,
                Position = 1024
            }
        };

        var patches = new[]
        {
            new MotorStatePatch
            {
                Id = 1,
                Position = 2048
            }
        };

        MotorStateMerger.Apply(motors, patches);

        Assert.AreEqual(2048, motors[0].Position, 0.1);
        Assert.AreEqual(0, motors[0].MinAngle, 0.1);
        Assert.AreEqual(180, motors[0].MaxAngle, 0.1);
        Assert.AreEqual(0, motors[0].MinRaw, 0.1);
        Assert.AreEqual(3072, motors[0].MaxRaw, 0.1);
    }
}
