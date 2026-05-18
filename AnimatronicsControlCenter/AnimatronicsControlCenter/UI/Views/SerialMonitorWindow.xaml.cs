using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.UI.Helpers;
using Windows.UI;
using WinRT.Interop;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class SerialMonitorWindow : Window
    {
        public SerialMonitorWindow()
        {
            InitializeComponent();

            this.SystemBackdrop = new MicaBackdrop();
            ApplyTheme();
            RootGrid.ActualThemeChanged += (_, _) => ApplyTitleBarColors();

            var localizationService = App.Current.Services.GetRequiredService<ILocalizationService>();
            this.Title = localizationService.GetString("SerialMonitor_Title");
        }

        public void ApplyTheme()
        {
            var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
            RootGrid.RequestedTheme = AppThemeHelper.ToElementTheme(settingsService.Theme);
            ApplyTitleBarColors();
        }

        private void ApplyTitleBarColors()
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
            {
                return;
            }

            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            bool isLight = RootGrid.ActualTheme == ElementTheme.Light;
            appWindow.TitleBar.BackgroundColor = isLight ? Color.FromArgb(255, 243, 243, 243) : Color.FromArgb(255, 32, 32, 32);
            appWindow.TitleBar.ForegroundColor = isLight ? Colors.Black : Colors.White;
            appWindow.TitleBar.InactiveBackgroundColor = isLight ? Color.FromArgb(255, 235, 235, 235) : Color.FromArgb(255, 45, 45, 45);
            appWindow.TitleBar.InactiveForegroundColor = isLight ? Color.FromArgb(255, 96, 96, 96) : Color.FromArgb(255, 190, 190, 190);

            appWindow.TitleBar.ButtonBackgroundColor = appWindow.TitleBar.BackgroundColor;
            appWindow.TitleBar.ButtonForegroundColor = appWindow.TitleBar.ForegroundColor;
            appWindow.TitleBar.ButtonHoverBackgroundColor = isLight ? Color.FromArgb(255, 225, 225, 225) : Color.FromArgb(255, 60, 60, 60);
            appWindow.TitleBar.ButtonHoverForegroundColor = appWindow.TitleBar.ForegroundColor;
            appWindow.TitleBar.ButtonPressedBackgroundColor = isLight ? Color.FromArgb(255, 210, 210, 210) : Color.FromArgb(255, 25, 25, 25);
            appWindow.TitleBar.ButtonPressedForegroundColor = appWindow.TitleBar.ForegroundColor;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = appWindow.TitleBar.InactiveBackgroundColor;
            appWindow.TitleBar.ButtonInactiveForegroundColor = isLight ? Color.FromArgb(255, 112, 112, 112) : Color.FromArgb(255, 170, 170, 170);
        }
    }
}
