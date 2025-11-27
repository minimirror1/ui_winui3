using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using AnimatronicsControlCenter.UI.Views;
using System.Linq;
using Microsoft.UI.Xaml.Media;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            UpdateLanguage(); // Set initial strings
            
            this.SystemBackdrop = new MicaBackdrop();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            
            ContentFrame.Navigated += ContentFrame_Navigated;
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
    }
}
