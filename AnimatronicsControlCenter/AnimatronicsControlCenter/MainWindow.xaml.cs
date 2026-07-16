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
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using System.ComponentModel;

namespace AnimatronicsControlCenter
{
    public sealed partial class MainWindow : Window
    {
        private static readonly SolidColorBrush DarkInactiveTrafficBrush = new(ColorHelper.FromArgb(0xFF, 0x73, 0x73, 0x73));
        private static readonly SolidColorBrush LightInactiveTrafficBrush = new(ColorHelper.FromArgb(0xFF, 0x78, 0x78, 0x78));
        private static readonly SolidColorBrush RxActiveTrafficBrush = new(ColorHelper.FromArgb(0xFF, 0x33, 0xD1, 0xC4));
        private static readonly SolidColorBrush TxActiveTrafficBrush = new(ColorHelper.FromArgb(0xFF, 0xFF, 0xA3, 0x3A));
        private static readonly SolidColorBrush ServerOnlineBrush = new(ColorHelper.FromArgb(0xFF, 0x4C, 0xD9, 0x64));
        private static readonly SolidColorBrush DarkIdleTrafficChromeBrush = new(ColorHelper.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
        private static readonly SolidColorBrush LightIdleTrafficChromeBrush = new(ColorHelper.FromArgb(0x14, 0x00, 0x00, 0x00));
        private static readonly SolidColorBrush DarkActiveTrafficChromeBrush = new(ColorHelper.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
        private static readonly SolidColorBrush LightActiveTrafficChromeBrush = new(ColorHelper.FromArgb(0x1F, 0x00, 0x00, 0x00));

        private readonly ISerialTrafficTap _serialTrafficTap;
        private readonly IBackendTrafficTap _backendTrafficTap;
        private readonly IBackendPowerSseService _backendPowerSseService;
        private readonly IOperatingHoursAutoSyncService _operatingHoursAutoSyncService;
        private readonly SerialMonitorWindowHost _serialMonitorWindowHost;
        private readonly ISettingsService _settingsService;
        private readonly SerialTrafficIndicatorState _serialTrafficIndicatorState = new();
        private readonly DispatcherTimer _serialTrafficIndicatorTimer;
        private readonly DispatcherTimer _serverTrafficIndicatorTimer;
        private readonly object _serialTrafficIndicatorLock = new();

        public SettingsViewModel ConnectionViewModel { get; }

        public MainWindow(ISerialTrafficTap serialTrafficTap, IBackendTrafficTap backendTrafficTap, IBackendPowerSseService backendPowerSseService, IOperatingHoursAutoSyncService operatingHoursAutoSyncService, SerialMonitorWindowHost serialMonitorWindowHost, ISettingsService settingsService, SettingsViewModel settingsViewModel)
        {
            _serialTrafficTap = serialTrafficTap;
            _backendTrafficTap = backendTrafficTap;
            _backendPowerSseService = backendPowerSseService;
            _operatingHoursAutoSyncService = operatingHoursAutoSyncService;
            _serialMonitorWindowHost = serialMonitorWindowHost;
            _settingsService = settingsService;
            ConnectionViewModel = settingsViewModel;

            this.InitializeComponent();
            ApplyTheme();
            UpdateLanguage(); // Set initial strings
            
            this.SystemBackdrop = new MicaBackdrop();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            _serialTrafficIndicatorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _serialTrafficIndicatorTimer.Tick += SerialTrafficIndicatorTimer_Tick;
            _serverTrafficIndicatorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _serverTrafficIndicatorTimer.Tick += ServerTrafficIndicatorTimer_Tick;

            _serialTrafficTap.EntryRecorded += SerialTrafficTap_EntryRecorded;
            _backendTrafficTap.TrafficChanged += BackendTrafficTap_TrafficChanged;
            Closed += MainWindow_Closed;
            ContentFrame.Navigated += ContentFrame_Navigated;
            RootGrid.ActualThemeChanged += RootGrid_ActualThemeChanged;

            UpdateSerialTrafficIndicator();
            UpdateServerTrafficIndicator();
            UpdateConnectionIconVisibility();
            ConnectionViewModel.PropertyChanged += ConnectionViewModel_PropertyChanged;
        }

        public void ApplyTheme()
        {
            RootGrid.RequestedTheme = AppThemeHelper.ToElementTheme(_settingsService.Theme);
            UpdateSerialTrafficIndicator();
            UpdateServerTrafficIndicator();
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
            else if (ContentFrame.SourcePageType == typeof(ServerMonitorPage)
                || ContentFrame.SourcePageType == typeof(BackendSettingsPage)
                || ContentFrame.SourcePageType == typeof(OperatingHoursSyncPage))
            {
                NavView.SelectedItem = null;
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

        private void ConnectionViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.IsConnectionActive))
            {
                UpdateConnectionIconVisibility();
            }
        }

        private void UpdateConnectionIconVisibility()
        {
            Visibility connectedVisibility = ConnectionViewModel.IsConnectionActive ? Visibility.Visible : Visibility.Collapsed;
            Visibility disconnectedVisibility = ConnectionViewModel.IsConnectionActive ? Visibility.Collapsed : Visibility.Visible;

            ConnectedPlugIcon.Visibility = connectedVisibility;
            FlyoutConnectedPlugIcon.Visibility = connectedVisibility;
            DisconnectedPlugIcon.Visibility = disconnectedVisibility;
            FlyoutDisconnectedPlugIcon.Visibility = disconnectedVisibility;
        }

        private void BackendSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(BackendSettingsPage));
        }

        private void RootGrid_ActualThemeChanged(FrameworkElement sender, object args)
        {
            UpdateSerialTrafficIndicator();
            UpdateServerTrafficIndicator();
        }

        private void ServerTrafficButton_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(ServerMonitorPage));
        }

        private void OperatingHoursSyncButton_Click(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(typeof(OperatingHoursSyncPage));
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

            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, ActivateSerialTrafficIndicator);
        }

        private void BackendTrafficTap_TrafficChanged(object? sender, EventArgs e)
        {
            if (DispatcherQueue.HasThreadAccess)
            {
                ActivateServerTrafficIndicator();
                return;
            }

            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, ActivateServerTrafficIndicator);
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

        private void ActivateServerTrafficIndicator()
        {
            UpdateServerTrafficIndicator();
            if (!_serverTrafficIndicatorTimer.IsEnabled)
                _serverTrafficIndicatorTimer.Start();
        }

        private void ServerTrafficIndicatorTimer_Tick(object? sender, object e)
        {
            var snapshot = UpdateServerTrafficIndicator();
            if (!snapshot.IsUplinkActive && !snapshot.IsDownlinkActive)
                _serverTrafficIndicatorTimer.Stop();
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

        private BackendTrafficSnapshot UpdateServerTrafficIndicator()
        {
            var snapshot = _backendTrafficTap.GetSnapshot(DateTimeOffset.Now);

            ServerStatusDot.Background = snapshot.IsServerOnline ? ServerOnlineBrush : InactiveTrafficBrush;
            ServerUplinkDot.Background = snapshot.IsUplinkActive ? TxActiveTrafficBrush : InactiveTrafficBrush;
            ServerDownlinkDot.Background = snapshot.IsDownlinkActive ? RxActiveTrafficBrush : InactiveTrafficBrush;
            ServerStatusDot.Opacity = snapshot.IsServerOnline ? 1.0 : 0.45;
            ServerUplinkDot.Opacity = snapshot.IsUplinkActive ? 1.0 : 0.45;
            ServerDownlinkDot.Opacity = snapshot.IsDownlinkActive ? 1.0 : 0.45;
            ServerTrafficButtonChrome.Background = snapshot.IsServerOnline || snapshot.IsUplinkActive || snapshot.IsDownlinkActive
                ? ActiveTrafficChromeBrush
                : IdleTrafficChromeBrush;

            ToolTipService.SetToolTip(ServerTrafficButton, BuildServerTrafficToolTip(snapshot));
            return snapshot;
        }

        private static string BuildSerialTrafficToolTip(SerialTrafficIndicatorSnapshot snapshot)
        {
            string rx = snapshot.IsRxActive ? "RX active" : "RX idle";
            string tx = snapshot.IsTxActive ? "TX active" : "TX idle";
            return $"Serial activity\n{rx}\n{tx}\nClick to open monitor";
        }

        private static string BuildServerTrafficToolTip(BackendTrafficSnapshot snapshot)
        {
            string status = snapshot.IsServerOnline ? "Server online" : "Server offline";
            string uplink = snapshot.IsUplinkActive ? "Uplink active" : "Uplink idle";
            string downlink = snapshot.IsDownlinkActive ? "Downlink active" : "Downlink idle";
            return $"Server activity\n{status}\n{uplink}\n{downlink}\nClick to open monitor";
        }

        private SolidColorBrush InactiveTrafficBrush
            => RootGrid.ActualTheme == ElementTheme.Light ? LightInactiveTrafficBrush : DarkInactiveTrafficBrush;

        private SolidColorBrush IdleTrafficChromeBrush
            => RootGrid.ActualTheme == ElementTheme.Light ? LightIdleTrafficChromeBrush : DarkIdleTrafficChromeBrush;

        private SolidColorBrush ActiveTrafficChromeBrush
            => RootGrid.ActualTheme == ElementTheme.Light ? LightActiveTrafficChromeBrush : DarkActiveTrafficChromeBrush;

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            ConnectionViewModel.PropertyChanged -= ConnectionViewModel_PropertyChanged;
            _serialTrafficTap.EntryRecorded -= SerialTrafficTap_EntryRecorded;
            _backendTrafficTap.TrafficChanged -= BackendTrafficTap_TrafficChanged;
            _serialTrafficIndicatorTimer.Tick -= SerialTrafficIndicatorTimer_Tick;
            _serialTrafficIndicatorTimer.Stop();
            _serverTrafficIndicatorTimer.Tick -= ServerTrafficIndicatorTimer_Tick;
            _serverTrafficIndicatorTimer.Stop();
            _backendPowerSseService.Stop();
            _operatingHoursAutoSyncService.Stop();
            RootGrid.ActualThemeChanged -= RootGrid_ActualThemeChanged;
            Closed -= MainWindow_Closed;
        }
    }
}
