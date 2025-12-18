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
            MotionTotalTime = TimeSpan.Zero;
            MotionDataCount = 0;
            MotionCreatedAt = default;
        }
    }
}
