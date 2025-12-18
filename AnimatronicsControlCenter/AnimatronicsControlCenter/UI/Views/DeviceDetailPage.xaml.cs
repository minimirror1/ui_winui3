using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.UI.Xaml.Input;
using AnimatronicsControlCenter.Core.Models;
using Microsoft.UI.Xaml.Navigation;
using System.ComponentModel;
using System;
using System.Collections.Generic;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class DeviceDetailPage : Page
    {
        public DeviceDetailViewModel ViewModel { get; }

        public DeviceDetailPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<DeviceDetailViewModel>();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Device device)
            {
                ViewModel.SelectedDevice = device;
            }

            // Default pivot is Overview.
            ViewModel.SetMotorsPollingAllowed(true);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            ViewModel.StopMotorsPolling();
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.Files))
            {
                // UpdateFileTree(); // No longer needed as we bind ItemsSource
            }
            else if (e.PropertyName == nameof(ViewModel.IsVerificationDialogOpen))
            {
                if (ViewModel.IsVerificationDialogOpen)
                {
                    _ = VerificationDialog.ShowAsync();
                    // We need to handle closing/resetting state if the dialog is dismissed by user action on dialog itself
                    // But here we just show it. The VM closes it logic is separate, or we reset it here.
                }
                else
                {
                    VerificationDialog.Hide();
                }
            }
        }

        private void UpdateFileTree()
        {
            FileTreeView.RootNodes.Clear();
            foreach (var item in ViewModel.Files)
            {
                FileTreeView.RootNodes.Add(CreateTreeNode(item));
            }
        }

        private TreeViewNode CreateTreeNode(FileSystemItem item)
        {
            var node = new TreeViewNode
            {
                Content = item,
                IsExpanded = false
            };
            
            if (item.IsDirectory && item.Children != null)
            {
                foreach (var child in item.Children)
                {
                    node.Children.Add(CreateTreeNode(child));
                }
            }

            return node;
        }

        private void Slider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Slider slider && slider.DataContext is MotorState motor)
            {
                ViewModel.MoveMotorCommand.Execute(motor);
            }
        }

        private void MotionSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Slider slider)
            {
                ViewModel.SeekMotionCommand.Execute(slider.Value);
            }
        }

        private void FileTreeView_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is TreeViewNode node && node.Content is FileSystemItem item)
            {
                ViewModel.SelectedFile = item;
            }
            else if (args.InvokedItem is FileSystemItem fileItem)
            {
                 // Depending on how TreeView is populated, InvokedItem might be the content directly
                 ViewModel.SelectedFile = fileItem;
            }
        }

        private void MainPivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Only poll in Overview.
            var isOverview = MainPivot.SelectedIndex == 0;
            ViewModel.SetMotorsPollingAllowed(isOverview);
        }
    }
}
