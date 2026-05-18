using System;
using System.Collections.Generic;
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
            ViewModel.Packets.CollectionChanged += Packets_CollectionChanged;
            ViewModel.ComRawEntries.CollectionChanged += ComRawEntries_CollectionChanged;
            Unloaded += SerialMonitorPage_Unloaded;
        }

        private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!ViewModel.IsAutoScrollEnabled) return;
            if (ViewModel.IsPaused) return;
            if (ViewModel.SelectedTabIndex != 0) return;

            ScrollToBottomAfterLayout(SerialScroll);
        }

        private void Packets_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!ViewModel.IsAutoScrollEnabled) return;
            if (ViewModel.IsPaused) return;
            if (ViewModel.SelectedTabIndex != 1) return;

            ScrollListToLastItemAfterLayout(PacketList, ViewModel.Packets);
        }

        private void ComRawEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!ViewModel.IsAutoScrollEnabled) return;
            if (ViewModel.IsPaused) return;
            if (ViewModel.SelectedTabIndex != 2) return;

            ScrollToBottomAfterLayout(ComRawScroll);
        }

        private static void ScrollToBottomAfterLayout(ScrollViewer scrollViewer)
        {
            void ScrollToBottom()
            {
                scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null);
            }

            if (!scrollViewer.DispatcherQueue.TryEnqueue(ScrollToBottom))
            {
                ScrollToBottom();
            }
        }

        private static void ScrollListToLastItemAfterLayout<T>(ListView listView, IReadOnlyList<T> items)
        {
            void ScrollToLastItem()
            {
                if (items.Count == 0) return;
                listView.ScrollIntoView(items[^1]);
            }

            if (!listView.DispatcherQueue.TryEnqueue(ScrollToLastItem))
            {
                ScrollToLastItem();
            }
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

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.SelectedTabIndex = MainPivot.SelectedIndex;
        }

        private void SerialMonitorPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.IsComRawCaptureEnabled = false;
        }

        private Visibility BoolToVisible(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
        private Visibility BoolToCollapsed(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

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




