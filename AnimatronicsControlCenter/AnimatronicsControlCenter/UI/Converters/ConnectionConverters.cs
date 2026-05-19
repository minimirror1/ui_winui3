using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.UI.Helpers;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;
using System;

namespace AnimatronicsControlCenter.UI.Converters
{
    public class BoolToColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isConnected && isConnected)
            {
                // Green for connected
                return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 76, 201, 128));
            }
            // Gray/Default for disconnected
            return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class MotorErrorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool isError && isError)
                return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 53, 69));
            return new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
            => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class DeviceCardStatusToBrushConverter : IValueConverter
    {
        // Playing: accent blue, Ready: success green, Idle: muted white, Fault: critical red
        private static readonly SolidColorBrush Playing = new(Windows.UI.Color.FromArgb(255, 96, 205, 255));
        private static readonly SolidColorBrush Ready   = new(Windows.UI.Color.FromArgb(255, 108, 203, 95));
        private static readonly SolidColorBrush Fault   = new(Windows.UI.Color.FromArgb(255, 255, 153, 164));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is DeviceCardStatus s ? s switch
            {
                DeviceCardStatus.Playing => Playing,
                DeviceCardStatus.Ready   => Ready,
                DeviceCardStatus.Fault   => Fault,
                _                        => ThemeAwareBrushes.NeutralBrush()
            } : ThemeAwareBrushes.NeutralBrush();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class DeviceCardStatusToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is DeviceCardStatus s ? s switch
            {
                DeviceCardStatus.Playing => "Playing",
                DeviceCardStatus.Ready   => "Ready",
                DeviceCardStatus.Fault   => "Fault",
                _                        => "Idle"
            } : "Idle";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class MotionStateToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Active  = new(Windows.UI.Color.FromArgb(255, 96, 205, 255));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is MotionState s && (s == MotionState.Playing || s == MotionState.Paused)
                ? Active : ThemeAwareBrushes.NeutralBrush();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class PowerStatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush On      = new(Windows.UI.Color.FromArgb(255, 108, 203, 95));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is string s && s.Equals("ON", StringComparison.OrdinalIgnoreCase) ? On : ThemeAwareBrushes.NeutralBrush();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class MotionStateToButtonLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is MotionState s && s == MotionState.Playing ? "Stop" : "Play";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class MotionStateToButtonIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is MotionState s && s == MotionState.Playing ? "\uE769" : "\uE768";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class TimeSpanToShortStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is TimeSpan t
                ? $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}"
                : "00:00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    public class IntToPaddedIdConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is int i ? i.ToString("D2") : "00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    /// <summary>
    /// Relay LED/text color: ON → danger red, OFF → muted gray
    /// </summary>
    public class PowerStatusToDangerBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush On  = new(Windows.UI.Color.FromArgb(255, 248, 113, 113)); // red-400

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is string s && s.Equals("ON", StringComparison.OrdinalIgnoreCase) ? On : ThemeAwareBrushes.NeutralBrush();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    /// <summary>
    /// Relay lock state: true (unlocked) → accent green, false → muted
    /// </summary>
    public class BoolToRelayLockBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Unlocked = new(Windows.UI.Color.FromArgb(255, 108, 203, 95));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value is bool b && b ? Unlocked : ThemeAwareBrushes.NeutralBrush();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }

    internal static class ThemeAwareBrushes
    {
        public static SolidColorBrush NeutralBrush()
            => AppThemeHelper.CreateBrushForCurrentTheme(
                Windows.UI.Color.FromArgb(92, 255, 255, 255),
                Windows.UI.Color.FromArgb(255, 107, 114, 128));
    }

    // ─── Operating hours converters ─────────────────────────────────────────────

    /// <summary>
    /// Returns a semi-transparent amber background for diff rows, transparent otherwise.
    /// </summary>
    public class OpsDiffHighlightConverter : IValueConverter
    {
        private static readonly SolidColorBrush Amber       = new(Windows.UI.Color.FromArgb(45, 251, 191, 36));
        private static readonly SolidColorBrush Transparent = new(Windows.UI.Color.FromArgb(0, 0, 0, 0));

        public object Convert(object value, Type targetType, object parameter, string language)
            => value is bool b && b ? (object)Amber : Transparent;

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Colors the status badge based on DeviceCompareStatus:
    /// Match=green, Diff=amber, Error=red.
    /// </summary>
    public class OpsCompareStatusToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Match = new(Windows.UI.Color.FromArgb(255, 74,  222, 128));
        private static readonly SolidColorBrush Diff  = new(Windows.UI.Color.FromArgb(255, 251, 191, 36));
        private static readonly SolidColorBrush Err   = new(Windows.UI.Color.FromArgb(255, 239, 68,  68));

        public object Convert(object value, Type targetType, object parameter, string language)
            => value is DeviceCompareStatus s ? s switch
            {
                DeviceCompareStatus.Match => (object)Match,
                DeviceCompareStatus.Diff  => Diff,
                _                         => Err,
            } : (object)Err;

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Colors diff-day row text (server or device column): amber when diff, muted otherwise.
    /// </summary>
    public class OpsDiffDayTextBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Amber = new(Windows.UI.Color.FromArgb(255, 251, 191, 36));
        private static readonly SolidColorBrush Muted = new(Windows.UI.Color.FromArgb(255, 156, 163, 175));

        public object Convert(object value, Type targetType, object parameter, string language)
            => value is bool b && b ? (object)Amber : Muted;

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Device status badge: ok=green, diff=amber, err=red, idle=muted.
    /// </summary>
    public class OpsDeviceStatusBadgeToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush Ok    = new(Windows.UI.Color.FromArgb(255, 74,  222, 128));
        private static readonly SolidColorBrush Diff  = new(Windows.UI.Color.FromArgb(255, 251, 191, 36));
        private static readonly SolidColorBrush Err   = new(Windows.UI.Color.FromArgb(255, 239, 68,  68));
        private static readonly SolidColorBrush Idle  = new(Windows.UI.Color.FromArgb(120, 156, 163, 175));

        public object Convert(object value, Type targetType, object parameter, string language)
            => value is string s ? s switch
            {
                "ok"   => (object)Ok,
                "diff" => Diff,
                "err"  => Err,
                _      => Idle,
            } : (object)Idle;

        public object ConvertBack(object value, Type targetType, object parameter, string language)
            => throw new NotImplementedException();
    }
}









