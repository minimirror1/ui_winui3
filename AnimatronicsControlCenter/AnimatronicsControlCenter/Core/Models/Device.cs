using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System;

namespace AnimatronicsControlCenter.Core.Models
{
    public enum MotionState
    {
        Idle,
        Playing,
        Paused,
        Stopped
    }

    public partial class Device : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private bool isConnected;

        [ObservableProperty]
        private string statusMessage;

        [ObservableProperty]
        private TimeSpan motionTotalTime;

        [ObservableProperty]
        private int motionDataCount;

        [ObservableProperty]
        private DateTime motionCreatedAt;

        [ObservableProperty]
        private MotionState motionState;

        [ObservableProperty]
        private TimeSpan motionCurrentTime;

        public ObservableCollection<MotorState> Motors { get; } = new();

        public Device(int id)
        {
            Id = id;
            IsConnected = false;
            StatusMessage = "Unknown";
            MotionState = MotionState.Idle;
            MotionCurrentTime = TimeSpan.Zero;
            MotionTotalTime = TimeSpan.FromMinutes(2.5); // Mock total time
            
            // Mock motors for testing
            // Using ID logic x-y. Let's make Group 1, Sub 1 and Group 1, Sub 2 etc.
            Motors.Add(new MotorState { Id = 1, GroupId = 1, SubId = 1, Position = 90, Type = "Servo", Status = "Normal" });
            Motors.Add(new MotorState { Id = 2, GroupId = 1, SubId = 2, Position = 45, Type = "DC", Status = "Error" });
            Motors.Add(new MotorState { Id = 3, GroupId = 2, SubId = 1, Position = 0, Type = "Stepper", Status = "Normal" });
        }
    }
}
