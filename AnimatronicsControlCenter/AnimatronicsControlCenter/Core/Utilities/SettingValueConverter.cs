using System;
using System.Globalization;

namespace AnimatronicsControlCenter.Core.Utilities
{
    public static class SettingValueConverter
    {
        public static double ReadDouble(object? value, double fallback)
        {
            return value switch
            {
                null => fallback,
                double d => d,
                float f => f,
                decimal m => (double)m,
                int i => i,
                long l => l,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariantParsed) => invariantParsed,
                string s when double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out var cultureParsed) => cultureParsed,
                _ => fallback
            };
        }
    }
}
