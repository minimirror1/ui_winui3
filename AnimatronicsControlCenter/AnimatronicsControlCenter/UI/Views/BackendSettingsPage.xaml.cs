using System;
using System.Collections.Generic;
using System.Linq;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class BackendSettingsPage : Page
{
    public BackendSettingsViewModel ViewModel { get; }

    public BackendSettingsPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<BackendSettingsViewModel>();
    }

    private async void OnOpenRegistrationDialogClicked(object sender, RoutedEventArgs e)
    {
        var catalogClient = App.Current.Services.GetRequiredService<IBackendServerCatalogClient>();
        var stores = ViewModel.ServerStoreList.ToList();
        var vm = new BackendRegistrationViewModel(catalogClient, stores);
        var dialog = new BackendRegistrationDialog(vm);
        if (App.Current.m_window?.Content is FrameworkElement element)
            dialog.XamlRoot = element.XamlRoot;
        await dialog.ShowAsync().AsTask();
        if (vm.Result is { } result)
            await ViewModel.HandleRegistrationResultAsync(result);
    }
}
