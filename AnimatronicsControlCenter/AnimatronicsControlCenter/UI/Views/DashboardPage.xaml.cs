using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.UI.ViewModels;
using AnimatronicsControlCenter.Core.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

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

        private void Card_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement card)
            {
                return;
            }

            card.SetValue(Panel.BackgroundProperty, Resources["DashboardDeviceCardHoverBackgroundBrush"]);
            SetPlayButtonHoverState(card, isHovered: true);
        }

        private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement card)
            {
                return;
            }

            card.SetValue(Panel.BackgroundProperty, Resources["DashboardDeviceCardBackgroundBrush"]);
            SetPlayButtonHoverState(card, isHovered: false);
        }

        private void SetPlayButtonHoverState(DependencyObject root, bool isHovered)
        {
            if (FindDescendantByName<Button>(root, "PlayStopButton") is not Button button)
            {
                return;
            }

            button.Background = (Brush)Resources[isHovered
                ? "DashboardPlayButtonHoverBackgroundBrush"
                : "DashboardPlayButtonBackgroundBrush"];
            button.BorderBrush = (Brush)Resources[isHovered
                ? "DashboardPlayButtonHoverBackgroundBrush"
                : "DashboardPlayButtonBorderBrush"];
            SetTextForeground(button, (Brush)Resources[isHovered
                ? "DashboardPlayButtonHoverForegroundBrush"
                : "DashboardPlayButtonForegroundBrush"]);
        }

        private static T? FindDescendantByName<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is T element && element.Name == name)
                {
                    return element;
                }

                T? match = FindDescendantByName<T>(child, name);
                if (match is not null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void SetTextForeground(DependencyObject root, Brush foreground)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                if (child is TextBlock textBlock)
                {
                    textBlock.Foreground = foreground;
                }
                else if (child is FontIcon fontIcon)
                {
                    fontIcon.Foreground = foreground;
                }

                SetTextForeground(child, foreground);
            }
        }
    }
}
