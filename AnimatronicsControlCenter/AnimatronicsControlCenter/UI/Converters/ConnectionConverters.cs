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

    public class BoolToIconGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            // E701: World (Connected like), E711: World (Disconnected like) or specific status icons
            // E930: CompletedSolid
            // EA39: ErrorBadge
            if (value is bool isConnected && isConnected)
            {
                return "\uE930"; // Checkmark circle
            }
            return "\uF384"; // StatusErrorFull (or similar circle)
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
    }
}







