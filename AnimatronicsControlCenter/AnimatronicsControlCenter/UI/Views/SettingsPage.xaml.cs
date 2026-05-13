using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.UI.ViewModels;
using System;
using System.Diagnostics;
using System.IO;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
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
    }
}

