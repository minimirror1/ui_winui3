using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AnimatronicsControlCenter.Core.Interfaces;
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

            var localizationService = App.Current.Services.GetRequiredService<ILocalizationService>();
            this.Title = localizationService.GetString("SerialMonitor_Title");

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

            appWindow.TitleBar.BackgroundColor = Color.FromArgb(255, 32, 32, 32);
            appWindow.TitleBar.ForegroundColor = Colors.White;
            appWindow.TitleBar.InactiveBackgroundColor = Color.FromArgb(255, 45, 45, 45);
            appWindow.TitleBar.InactiveForegroundColor = Color.FromArgb(255, 190, 190, 190);

            appWindow.TitleBar.ButtonBackgroundColor = Color.FromArgb(255, 32, 32, 32);
            appWindow.TitleBar.ButtonForegroundColor = Colors.White;
            appWindow.TitleBar.ButtonHoverBackgroundColor = Color.FromArgb(255, 60, 60, 60);
            appWindow.TitleBar.ButtonHoverForegroundColor = Colors.White;
            appWindow.TitleBar.ButtonPressedBackgroundColor = Color.FromArgb(255, 25, 25, 25);
            appWindow.TitleBar.ButtonPressedForegroundColor = Colors.White;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Color.FromArgb(255, 45, 45, 45);
            appWindow.TitleBar.ButtonInactiveForegroundColor = Color.FromArgb(255, 170, 170, 170);
        }
    }
}
