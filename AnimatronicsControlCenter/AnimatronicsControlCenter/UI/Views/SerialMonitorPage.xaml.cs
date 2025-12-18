using System;
using System.Collections.Specialized;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.UI.ViewModels;
using WinRT.Interop;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class SerialMonitorPage : Page
    {
        public SerialMonitorViewModel ViewModel { get; }

        public SerialMonitorPage()
        {
            InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<SerialMonitorViewModel>();
            DataContext = ViewModel;

            ViewModel.Entries.CollectionChanged += Entries_CollectionChanged;
        }

        private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!ViewModel.IsAutoScrollEnabled) return;
            if (ViewModel.IsPaused) return;

            // ItemsRepeater doesn't support ScrollIntoView; scroll the viewer to bottom.
            SerialScroll.ChangeView(null, SerialScroll.ScrollableHeight, null);
        }

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox comboBox) return;

            // Only update filter when the user actually changes selection.
            // This avoids TwoWay SelectedIndex pushing transient values and triggering list rebuild flicker.
            var idx = comboBox.SelectedIndex;
            if (idx < 0 || idx > 2) return;

            ViewModel.Filter = (SerialTrafficFilter)idx;
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Use the main window handle for picker ownership if we cannot access the current Window handle.
            var hwnd = App.Current.m_window != null
                ? WindowNative.GetWindowHandle(App.Current.m_window)
                : nint.Zero;

            await ViewModel.SaveToFileAsync(hwnd);
        }
    }
}


