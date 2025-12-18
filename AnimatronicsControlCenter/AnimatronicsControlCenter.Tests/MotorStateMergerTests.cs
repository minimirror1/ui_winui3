using System.Collections.ObjectModel;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Motors;

namespace AnimatronicsControlCenter.Tests;

public class MotorStateMergerTests
{
    [Fact]
    public void Apply_CreatesNewMotors_FromSnapshot()
    {
        var motors = new ObservableCollection<MotorState>();

        MotorStateMerger.Apply(
            motors,
            new[]
            {
                new MotorStatePatch { Id = 1, GroupId = 1, SubId = 1, Type = "Servo", Status = "Normal", Position = 90, Velocity = 0.5 },
                new MotorStatePatch { Id = 2, GroupId = 1, SubId = 2, Type = "DC", Status = "Error", Position = 45, Velocity = 1.0 },
            });

        Assert.Equal(2, motors.Count);

        var m1 = motors.Single(m => m.Id == 1);
        Assert.Equal(1, m1.GroupId);
        Assert.Equal(1, m1.SubId);
        Assert.Equal("Servo", m1.Type);
        Assert.Equal("Normal", m1.Status);
        Assert.Equal(90, m1.Position);
        Assert.Equal(0.5, m1.Velocity);

        var m2 = motors.Single(m => m.Id == 2);
        Assert.Equal(1, m2.GroupId);
        Assert.Equal(2, m2.SubId);
        Assert.Equal("DC", m2.Type);
        Assert.Equal("Error", m2.Status);
        Assert.Equal(45, m2.Position);
        Assert.Equal(1.0, m2.Velocity);
    }

    [Fact]
    public void Apply_PartialUpdate_OnlyUpdatesProvidedFields()
    {
        var motors = new ObservableCollection<MotorState>
        {
            new MotorState { Id = 1, GroupId = 1, SubId = 1, Type = "Servo", Status = "Normal", Position = 10, Velocity = 2.0 }
        };

        MotorStateMerger.Apply(
            motors,
            new[]
            {
                new MotorStatePatch { Id = 1, Position = 99 } // only position
            });

        var m = motors.Single(x => x.Id == 1);
        Assert.Equal(1, m.GroupId);
        Assert.Equal(1, m.SubId);
        Assert.Equal("Servo", m.Type);
        Assert.Equal("Normal", m.Status);
        Assert.Equal(99, m.Position);
        Assert.Equal(2.0, m.Velocity);
    }

    [Fact]
    public void Apply_PartialUpdate_DoesNotRemoveOtherMotors()
    {
        var motors = new ObservableCollection<MotorState>
        {
            new MotorState { Id = 1, GroupId = 1, SubId = 1, Type = "Servo", Status = "Normal", Position = 10, Velocity = 0 },
            new MotorState { Id = 2, GroupId = 1, SubId = 2, Type = "DC", Status = "Normal", Position = 20, Velocity = 0 }
        };

        MotorStateMerger.Apply(
            motors,
            new[]
            {
                new MotorStatePatch { Id = 1, Status = "Error" } // does not mention motor #2
            });

        Assert.Equal(2, motors.Count);
        Assert.Equal("Error", motors.Single(x => x.Id == 1).Status);
        Assert.Equal("Normal", motors.Single(x => x.Id == 2).Status);
    }

    [Fact]
    public void Apply_FullUpdate_UpdatesAllMotors()
    {
        var motors = new ObservableCollection<MotorState>
        {
            new MotorState { Id = 1, GroupId = 1, SubId = 1, Type = "Servo", Status = "Normal", Position = 10, Velocity = 0 },
            new MotorState { Id = 2, GroupId = 1, SubId = 2, Type = "DC", Status = "Normal", Position = 20, Velocity = 0 }
        };

        MotorStateMerger.Apply(
            motors,
            new[]
            {
                new MotorStatePatch { Id = 1, Position = 111, Status = "Normal" },
                new MotorStatePatch { Id = 2, Position = 222, Status = "Error" }
            });

        Assert.Equal(111, motors.Single(x => x.Id == 1).Position);
        Assert.Equal(222, motors.Single(x => x.Id == 2).Position);
        Assert.Equal("Error", motors.Single(x => x.Id == 2).Status);
    }
}

