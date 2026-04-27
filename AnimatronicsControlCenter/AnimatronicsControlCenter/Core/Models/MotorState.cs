using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Globalization;

namespace AnimatronicsControlCenter.Core.Models
{
    public partial class MotorState : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private int groupId;

        [ObservableProperty]
        private int subId;

        [ObservableProperty]
        private string type = "Null"; // Default type

        [ObservableProperty]
        private string status = "Normal"; // Default status

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AnglePosition))]
        [NotifyPropertyChangedFor(nameof(PositionDisplay))]
        private double position;

        [ObservableProperty]
        private double velocity;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AnglePosition))]
        [NotifyPropertyChangedFor(nameof(PositionDisplay))]
        private double minAngle = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AnglePosition))]
        [NotifyPropertyChangedFor(nameof(PositionDisplay))]
        private double maxAngle = 180;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AnglePosition))]
        [NotifyPropertyChangedFor(nameof(PositionDisplay))]
        private double minRaw = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AnglePosition))]
        [NotifyPropertyChangedFor(nameof(PositionDisplay))]
        private double maxRaw = 180;

        public string DisplayId => $"{GroupId}-{SubId}";

        public double AnglePosition
        {
            get => ConvertRawToAngle(Position);
            set => Position = ConvertAngleToRaw(value);
        }

        public string PositionDisplay =>
            $"{AnglePosition.ToString("0.0", CultureInfo.InvariantCulture)}\u00B0({FormatRawValue(Position)})";

        private double ConvertRawToAngle(double rawValue)
        {
            if (!HasValidRange())
            {
                return rawValue;
            }

            var ratio = (rawValue - MinRaw) / (MaxRaw - MinRaw);
            var angle = MinAngle + (ratio * (MaxAngle - MinAngle));
            return Clamp(angle, MinAngle, MaxAngle);
        }

        private double ConvertAngleToRaw(double angleValue)
        {
            if (!HasValidRange())
            {
                return angleValue;
            }

            var clampedAngle = Clamp(angleValue, MinAngle, MaxAngle);
            var ratio = (clampedAngle - MinAngle) / (MaxAngle - MinAngle);
            var rawValue = MinRaw + (ratio * (MaxRaw - MinRaw));
            return Clamp(rawValue, MinRaw, MaxRaw);
        }

        private bool HasValidRange()
        {
            return Math.Abs(MaxAngle - MinAngle) > double.Epsilon
                && Math.Abs(MaxRaw - MinRaw) > double.Epsilon;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (minimum > maximum)
            {
                (minimum, maximum) = (maximum, minimum);
            }

            return Math.Min(Math.Max(value, minimum), maximum);
        }

        private static string FormatRawValue(double rawValue)
        {
            var rounded = Math.Round(rawValue);
            return Math.Abs(rawValue - rounded) < 0.0001
                ? rounded.ToString("0", CultureInfo.InvariantCulture)
                : rawValue.ToString("0.0", CultureInfo.InvariantCulture);
        }
    }
}
