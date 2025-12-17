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

            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ViewModel.SelectedEntry))
                {
                    if (ViewModel.SelectedEntry != null)
                    {
                        SerialList.ScrollIntoView(ViewModel.SelectedEntry);
                    }
                }
            };

            ViewModel.Entries.CollectionChanged += Entries_CollectionChanged;
        }

        private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!ViewModel.IsAutoScrollEnabled) return;
            if (ViewModel.IsPaused) return;
            if (ViewModel.Entries.Count == 0) return;

            SerialList.ScrollIntoView(ViewModel.Entries[^1]);
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

