using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.UI.ViewModels;
using AnimatronicsControlCenter.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();
        }

        private Visibility BoolToVisibility(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

        private void Card_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is Device device)
            {
                Frame.Navigate(typeof(DeviceDetailPage), device);
            }
        }

        private void DetailsButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is Device device)
            {
                Frame.Navigate(typeof(DeviceDetailPage), device);
            }
        }

        private void PlayStopButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: connect play/stop command
        }
    }
}
