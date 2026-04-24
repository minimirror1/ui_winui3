using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using AnimatronicsControlCenter.UI.Views;
using System.Linq;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.UI.Helpers;
using Microsoft.UI;
using Microsoft.UI.Dispatching;

namespace AnimatronicsControlCenter
{
    public sealed partial class MainWindow : Window
    {
        private static readonly SolidColorBrush InactiveTrafficBrush = new(ColorHelper.FromArgb(0xFF, 0x73, 0x73, 0x73));
        private static readonly SolidColorBrush RxActiveTrafficBrush = new(ColorHelper.FromArgb(0xFF, 0x33, 0xD1, 0xC4));
        private static readonly SolidColorBrush TxActiveTrafficBrush = new(ColorHelper.FromArgb(0xFF, 0xFF, 0xA3, 0x3A));
        private static readonly SolidColorBrush IdleTrafficChromeBrush = new(ColorHelper.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
        private static readonly SolidColorBrush ActiveTrafficChromeBrush = new(ColorHelper.FromArgb(0x28, 0xFF, 0xFF, 0xFF));

        private readonly ISerialTrafficTap _serialTrafficTap;
        private readonly SerialMonitorWindowHost _serialMonitorWindowHost;
        private readonly SerialTrafficIndicatorState _serialTrafficIndicatorState = new();
        private readonly DispatcherTimer _serialTrafficIndicatorTimer;
        private readonly object _serialTrafficIndicatorLock = new();

        public MainWindow(ISerialTrafficTap serialTrafficTap, SerialMonitorWindowHost serialMonitorWindowHost)
        {
            _serialTrafficTap = serialTrafficTap;
            _serialMonitorWindowHost = serialMonitorWindowHost;

            this.InitializeComponent();
            UpdateLanguage(); // Set initial strings
            
            this.SystemBackdrop = new MicaBackdrop();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            _serialTrafficIndicatorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _serialTrafficIndicatorTimer.Tick += SerialTrafficIndicatorTimer_Tick;

            _serialTrafficTap.EntryRecorded += SerialTrafficTap_EntryRecorded;
            Closed += MainWindow_Closed;
            ContentFrame.Navigated += ContentFrame_Navigated;

            UpdateSerialTrafficIndicator();
        }

        public void UpdateLanguage()
        {
            var localizationService = App.Current.Services.GetRequiredService<ILocalizationService>();

            // Update Title
            this.Title = localizationService.GetString("App_Title");

            // Update NavigationView Items
            foreach (var item in NavView.MenuItems.OfType<NavigationViewItem>())
            {
                if (item.Tag?.ToString() == "DashboardPage")
                {
                    item.Content = localizationService.GetString("Nav_Dashboard");
                }
            }

            var settingsItem = (NavigationViewItem)NavView.SettingsItem;
            if (settingsItem != null)
            {
                settingsItem.Content = localizationService.GetString("Nav_Settings");
            }

            // Reload current page to refresh x:Uid bindings
            if (ContentFrame.Content != null)
            {
                var pageType = ContentFrame.CurrentSourcePageType;
                ContentFrame.Navigate(pageType, null, new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
                if (ContentFrame.CanGoBack)
                {
                    ContentFrame.BackStack.RemoveAt(ContentFrame.BackStackDepth - 1);
                }
            }
        }

        private void ContentFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            BackButton.Visibility = ContentFrame.CanGoBack ? Visibility.Visible : Visibility.Collapsed;

            if (ContentFrame.SourcePageType == typeof(SettingsPage))
            {
                NavView.SelectedItem = (NavigationViewItem)NavView.SettingsItem;
            }
            else if (ContentFrame.SourcePageType == typeof(DashboardPage))
            {
                NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>()
                    .FirstOrDefault(i => i.Tag?.ToString() == "DashboardPage");
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (ContentFrame.CanGoBack)
            {
                ContentFrame.GoBack();
            }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            var firstItem = NavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
            if (firstItem != null)
            {
                NavView.SelectedItem = firstItem;
                ContentFrame.Navigate(typeof(DashboardPage));
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else if (args.SelectedItem is NavigationViewItem item && item.Tag != null)
            {
                Type? pageType = item.Tag.ToString() switch
                {
                    "DashboardPage" => typeof(DashboardPage),
                    _ => null
                };

                if (pageType != null)
                {
                    ContentFrame.Navigate(pageType);
                }
            }
        }

        private void SerialTrafficButton_Click(object sender, RoutedEventArgs e)
        {
            _serialMonitorWindowHost.Show();
        }

        private void SerialTrafficTap_EntryRecorded(object? sender, SerialTrafficEntry entry)
        {
            lock (_serialTrafficIndicatorLock)
            {
                _serialTrafficIndicatorState.Record(entry);
            }

            if (DispatcherQueue.HasThreadAccess)
            {
                ActivateSerialTrafficIndicator();
                return;
            }

            DispatcherQueue.TryEnqueue(ActivateSerialTrafficIndicator);
        }

        private void ActivateSerialTrafficIndicator()
        {
            UpdateSerialTrafficIndicator();
            if (!_serialTrafficIndicatorTimer.IsEnabled)
                _serialTrafficIndicatorTimer.Start();
        }

        private void SerialTrafficIndicatorTimer_Tick(object? sender, object e)
        {
            var snapshot = UpdateSerialTrafficIndicator();
            if (!snapshot.IsRxActive && !snapshot.IsTxActive)
                _serialTrafficIndicatorTimer.Stop();
        }

        private SerialTrafficIndicatorSnapshot UpdateSerialTrafficIndicator()
        {
            SerialTrafficIndicatorSnapshot snapshot;
            lock (_serialTrafficIndicatorLock)
            {
                snapshot = _serialTrafficIndicatorState.GetSnapshot(DateTimeOffset.Now);
            }

            RxTrafficDot.Background = snapshot.IsRxActive ? RxActiveTrafficBrush : InactiveTrafficBrush;
            TxTrafficDot.Background = snapshot.IsTxActive ? TxActiveTrafficBrush : InactiveTrafficBrush;
            RxTrafficDot.Opacity = snapshot.IsRxActive ? 1.0 : 0.45;
            TxTrafficDot.Opacity = snapshot.IsTxActive ? 1.0 : 0.45;
            SerialTrafficButtonChrome.Background = snapshot.IsRxActive || snapshot.IsTxActive
                ? ActiveTrafficChromeBrush
                : IdleTrafficChromeBrush;

            ToolTipService.SetToolTip(SerialTrafficButton, BuildSerialTrafficToolTip(snapshot));
            return snapshot;
        }

        private static string BuildSerialTrafficToolTip(SerialTrafficIndicatorSnapshot snapshot)
        {
            string rx = snapshot.IsRxActive ? "RX active" : "RX idle";
            string tx = snapshot.IsTxActive ? "TX active" : "TX idle";
            return $"Serial activity\n{rx}\n{tx}\nClick to open monitor";
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            _serialTrafficTap.EntryRecorded -= SerialTrafficTap_EntryRecorded;
            _serialTrafficIndicatorTimer.Tick -= SerialTrafficIndicatorTimer_Tick;
            _serialTrafficIndicatorTimer.Stop();
            Closed -= MainWindow_Closed;
        }
    }
}
