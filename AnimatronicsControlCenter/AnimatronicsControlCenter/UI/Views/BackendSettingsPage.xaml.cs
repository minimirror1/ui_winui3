using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        try
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
        catch (Exception ex)
        {
            ViewModel.ServerStatusMessage = $"데이터 관리 오류: {ex.Message}";
        }
    }

    private void OnOpenLocalSettingsFileClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var pathProvider = App.Current.Services.GetRequiredService<IBackendSettingsPathProvider>();
            string filePath = pathProvider.BackendSettingsFilePath;
            string? directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(filePath))
            {
                var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
                settingsService.Save();
            }

            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{filePath}\"")
            {
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            ViewModel.ServerStatusMessage = $"로컬 설정 파일 열기 오류: {ex.Message}";
        }
    }

    private Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;
}
