using System;
using AnimatronicsControlCenter.Core.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AnimatronicsControlCenter.UI.Converters
{
    public sealed class SerialDirectionToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush TxBrush = new(Colors.LimeGreen);
        private static readonly SolidColorBrush RxBrush = new(Colors.DeepSkyBlue);

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


