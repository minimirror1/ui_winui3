using System;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.UI.Helpers;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AnimatronicsControlCenter.UI.Converters
{
    public sealed class SerialDirectionToBrushConverter : IValueConverter
    {
        // TX → 파란색(송신), RX → 초록색(수신) — 디자인 명세에 따름
        private static readonly SolidColorBrush DarkTxBrush = new(Windows.UI.Color.FromArgb(255, 107, 163, 214));
        private static readonly SolidColorBrush DarkRxBrush = new(Windows.UI.Color.FromArgb(255, 110, 196, 160));
        private static readonly SolidColorBrush LightTxBrush = new(Windows.UI.Color.FromArgb(255, 37, 99, 235));
        private static readonly SolidColorBrush LightRxBrush = new(Windows.UI.Color.FromArgb(255, 4, 120, 87));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            bool isLight = AppThemeHelper.IsLightTheme();
            if (value is SerialTrafficDirection dir)
            {
                return dir == SerialTrafficDirection.Tx
                    ? isLight ? LightTxBrush : DarkTxBrush
                    : isLight ? LightRxBrush : DarkRxBrush;
            }

            return isLight ? LightRxBrush : DarkRxBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}






