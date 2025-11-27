using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using AnimatronicsControlCenter.UI.Views;
using System.Linq;
using Microsoft.UI.Xaml.Media;

namespace AnimatronicsControlCenter
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Animatronics Control Center";
            
            this.SystemBackdrop = new MicaBackdrop();
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
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
