using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.UI.Helpers;
using AnimatronicsControlCenter.UI.ViewModels;
using Windows.Graphics;
using WinRT.Interop;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class ReleaseNotesWindow : Window
    {
        public ReleaseNotesViewModel ViewModel { get; }

        public ReleaseNotesWindow(ReleaseNotesViewModel viewModel)
        {
            ViewModel = viewModel;

            InitializeComponent();

            this.SystemBackdrop = new MicaBackdrop();
            this.Title = "릴리즈 노트";

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            ApplyTheme();
            RootGrid.ActualThemeChanged += (_, _) => ApplyTitleBarColors();

            ResizeToDefault();
        }

        /// Window 에서는 x:Bind 에 StaticResource 컨버터를 쓸 수 없어 함수 바인딩으로 처리한다.
        public Visibility ToVisibility(bool value)
            => value ? Visibility.Visible : Visibility.Collapsed;

        public void ApplyTheme()
        {
            var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
            RootGrid.RequestedTheme = AppThemeHelper.ToElementTheme(settingsService.Theme);
            ApplyTitleBarColors();
        }

        private AppWindow? GetAppWindow()
        {
            IntPtr hwnd = WindowNative.GetWindowHandle(this);
            WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        private void ResizeToDefault()
        {
            GetAppWindow()?.Resize(new SizeInt32(1040, 760));
        }

        private void ApplyTitleBarColors()
        {
            if (!AppWindowTitleBar.IsCustomizationSupported())
            {
                return;
            }

            AppWindow? appWindow = GetAppWindow();
            if (appWindow is null)
            {
                return;
            }

            AppWindowTitleBar titleBar = appWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        }
    }
}
