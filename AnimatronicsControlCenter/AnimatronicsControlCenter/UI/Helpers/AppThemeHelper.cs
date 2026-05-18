using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace AnimatronicsControlCenter.UI.Helpers
{
    public static class AppThemeHelper
    {
        public const string DefaultTheme = "Default";
        public const string LightTheme = "Light";
        public const string DarkTheme = "Dark";

        public static ElementTheme ToElementTheme(string? theme)
            => NormalizeTheme(theme) switch
            {
                LightTheme => ElementTheme.Light,
                DarkTheme => ElementTheme.Dark,
                _ => ElementTheme.Default
            };

        public static string NormalizeTheme(string? theme)
        {
            if (string.Equals(theme, LightTheme, System.StringComparison.OrdinalIgnoreCase))
            {
                return LightTheme;
            }

            if (string.Equals(theme, DarkTheme, System.StringComparison.OrdinalIgnoreCase))
            {
                return DarkTheme;
            }

            return DefaultTheme;
        }

        public static bool IsLightTheme()
        {
            if (Application.Current is App app &&
                app.m_window?.Content is FrameworkElement root)
            {
                return root.ActualTheme == ElementTheme.Light;
            }

            return false;
        }

        public static SolidColorBrush CreateBrushForCurrentTheme(Color dark, Color light)
            => new(IsLightTheme() ? light : dark);
    }
}
