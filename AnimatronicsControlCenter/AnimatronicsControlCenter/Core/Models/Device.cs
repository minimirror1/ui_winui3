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

    public enum DeviceCardStatus
    {
        Idle,
        Ready,
        Playing,
        Fault
    }

    public partial class Device : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string name = string.Empty;

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

        [ObservableProperty]
        private ulong address64;

        [ObservableProperty]
        private string powerStatus = "OFF";

        [ObservableProperty]
        private bool hasError;

        public ObservableCollection<MotorState> Motors { get; } = new();

        public DeviceCardStatus CardStatus
        {
            get
            {
                if (!IsConnected || HasError) return DeviceCardStatus.Fault;
                return MotionState switch
                {
                    MotionState.Playing or MotionState.Paused => DeviceCardStatus.Playing,
                    MotionState.Idle => DeviceCardStatus.Idle,
                    _ => DeviceCardStatus.Ready
                };
            }
        }

        public double MotionProgress
        {
            get
            {
                if (MotionTotalTime.TotalSeconds <= 0) return 0.0;
                return Math.Clamp(MotionCurrentTime.TotalSeconds / MotionTotalTime.TotalSeconds, 0.0, 1.0);
            }
        }

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

        partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(CardStatus));
        partial void OnHasErrorChanged(bool value) => OnPropertyChanged(nameof(CardStatus));
        partial void OnMotionStateChanged(MotionState value) => OnPropertyChanged(nameof(CardStatus));
        partial void OnMotionCurrentTimeChanged(TimeSpan value) => OnPropertyChanged(nameof(MotionProgress));
        partial void OnMotionTotalTimeChanged(TimeSpan value) => OnPropertyChanged(nameof(MotionProgress));
    }
}
