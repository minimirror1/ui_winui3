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
            if (IsWithinNamedElement(e.OriginalSource as DependencyObject, "PlayStopButton"))
            {
                return;
            }

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

            card.SetValue(Panel.BackgroundProperty, GetDashboardBrush(card, "DashboardDeviceCardHoverBackgroundBrush"));
            SetPlayButtonHoverState(card, isHovered: true);
        }

        private void Card_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is not FrameworkElement card)
            {
                return;
            }

            card.SetValue(Panel.BackgroundProperty, GetDashboardBrush(card, "DashboardDeviceCardBackgroundBrush"));
            SetPlayButtonHoverState(card, isHovered: false);
        }

        private void SetPlayButtonHoverState(DependencyObject root, bool isHovered)
        {
            if (FindDescendantByName<Button>(root, "PlayStopButton") is not Button button)
            {
                return;
            }

            button.Background = GetDashboardBrush(button, isHovered
                ? "DashboardPlayButtonHoverBackgroundBrush"
                : "DashboardPlayButtonBackgroundBrush");
            button.BorderBrush = GetDashboardBrush(button, isHovered
                ? "DashboardPlayButtonHoverBackgroundBrush"
                : "DashboardPlayButtonBorderBrush");
            SetTextForeground(button, GetDashboardBrush(button, isHovered
                ? "DashboardPlayButtonHoverForegroundBrush"
                : "DashboardPlayButtonForegroundBrush"));
        }

        private Brush GetDashboardBrush(FrameworkElement element, string resourceKey)
        {
            string themeKey = element.ActualTheme == ElementTheme.Light ? "Light" : "Dark";
            if (Resources.ThemeDictionaries.TryGetValue(themeKey, out object? themeResources) &&
                themeResources is ResourceDictionary themeDictionary &&
                themeDictionary.TryGetValue(resourceKey, out object? brush) &&
                brush is Brush resolvedBrush)
            {
                return resolvedBrush;
            }

            return (Brush)Resources[resourceKey];
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

        private static bool IsWithinNamedElement(DependencyObject? source, string name)
        {
            for (DependencyObject? current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is FrameworkElement element && element.Name == name)
                {
                    return true;
                }
            }

            return false;
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
