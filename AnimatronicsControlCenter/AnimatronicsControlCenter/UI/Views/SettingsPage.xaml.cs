using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.UI.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.Globalization.NumberFormatting;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class SettingsPage : Page
    {
        private bool _isThemeRestartDialogOpen;

        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
            ViewModel.ThemeRestartRequested += ViewModel_ThemeRestartRequested;
            Unloaded += SettingsPage_Unloaded;
            ResponseTimeoutNumberBox.NumberFormatter = CreateOneDecimalFormatter();
            PingIntervalNumberBox.NumberFormatter = CreateOneDecimalFormatter();
        }

        private static DecimalFormatter CreateOneDecimalFormatter()
        {
            return new DecimalFormatter
            {
                FractionDigits = 1,
                NumberRounder = new IncrementNumberRounder
                {
                    Increment = 0.1,
                    RoundingAlgorithm = RoundingAlgorithm.RoundHalfUp
                }
            };
        }

        private void OnOpenAppSettingsFileClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
                string filePath = settingsService.AppSettingsFilePath;
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (!File.Exists(filePath))
                {
                    settingsService.Save();
                }

                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{filePath}\"")
                {
                    UseShellExecute = false
                });
            }
            catch
            {
            }
        }

        private void OnOpenAppSettingsFolderClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var settingsService = App.Current.Services.GetRequiredService<ISettingsService>();
                string? directory = Path.GetDirectoryName(settingsService.AppSettingsFilePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return;
                }

                Directory.CreateDirectory(directory);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{directory}\"")
                {
                    UseShellExecute = false
                });
            }
            catch
            {
            }
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            ViewModel.ThemeRestartRequested -= ViewModel_ThemeRestartRequested;
            Unloaded -= SettingsPage_Unloaded;
        }

        private async void ViewModel_ThemeRestartRequested(object? sender, EventArgs e)
        {
            await ShowThemeRestartDialogAsync();
        }

        private async Task ShowThemeRestartDialogAsync()
        {
            if (_isThemeRestartDialogOpen)
            {
                return;
            }

            _isThemeRestartDialogOpen = true;
            try
            {
                var dialog = new ContentDialog
                {
                    Title = ViewModel.Strings.Get("ThemeRestart_Title", ViewModel.Strings.Code),
                    Content = ViewModel.Strings.Get("ThemeRestart_Content", ViewModel.Strings.Code),
                    PrimaryButtonText = ViewModel.Strings.Get("ThemeRestartNow_Button", ViewModel.Strings.Code),
                    CloseButtonText = ViewModel.Strings.Get("ThemeRestartLater_Button", ViewModel.Strings.Code),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot
                };

                ContentDialogResult result = await dialog.ShowAsync().AsTask();
                if (result == ContentDialogResult.Primary)
                {
                    await RestartApplicationAsync();
                }
            }
            finally
            {
                _isThemeRestartDialogOpen = false;
            }
        }

        private async Task RestartApplicationAsync()
        {
            try
            {
                object? result = AppInstance.Restart(string.Empty);
                if (!string.Equals(result?.ToString(), "RestartPending", StringComparison.Ordinal))
                {
                    await ShowThemeRestartFailedDialogAsync(result?.ToString() ?? "Unknown");
                }
            }
            catch (Exception ex)
            {
                await ShowThemeRestartFailedDialogAsync(ex.Message);
            }
        }

        private async Task ShowThemeRestartFailedDialogAsync(string detail)
        {
            var dialog = new ContentDialog
            {
                Title = ViewModel.Strings.Get("ThemeRestartFailed_Title", ViewModel.Strings.Code),
                Content = $"{ViewModel.Strings.Get("ThemeRestartFailed_Content", ViewModel.Strings.Code)}\n\n{detail}",
                CloseButtonText = "OK",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync().AsTask();
        }
    }
}

