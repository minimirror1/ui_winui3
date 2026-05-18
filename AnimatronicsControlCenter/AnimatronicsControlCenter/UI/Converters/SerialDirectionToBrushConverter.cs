using System;
using AnimatronicsControlCenter.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AnimatronicsControlCenter.UI.Converters
{
    public sealed class SerialDirectionToBrushConverter : IValueConverter
    {
        // TX → 파란색(송신), RX → 초록색(수신) — 디자인 명세에 따름
        private static readonly SolidColorBrush TxBrush = new(Windows.UI.Color.FromArgb(255, 107, 163, 214));
        private static readonly SolidColorBrush RxBrush = new(Windows.UI.Color.FromArgb(255, 110, 196, 160));

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SerialTrafficDirection dir)
            {
                return dir == SerialTrafficDirection.Tx ? TxBrush : RxBrush;
            }
            return RxBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language) =>
            throw new NotSupportedException();
    }
}






