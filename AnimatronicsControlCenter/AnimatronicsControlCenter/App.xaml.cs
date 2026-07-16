using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Infrastructure;
using AnimatronicsControlCenter.UI.Helpers;
using AnimatronicsControlCenter.UI.ViewModels;
using AnimatronicsControlCenter.UI.Views;

namespace AnimatronicsControlCenter
{
    public partial class App : Application
    {
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; private set; }
        public Window? m_window;

        public App()
        {
            this.InitializeComponent();
            Services = ConfigureServices();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();
            settingsService.Load();
            
            var localizationService = Services.GetRequiredService<ILocalizationService>();
            localizationService.SetLanguage(settingsService.Language);
            
            m_window = Services.GetRequiredService<MainWindow>();
            Task<XamlRoot>? xamlRootTask = null;
            if (string.IsNullOrWhiteSpace(settingsService.BackendApiKey) &&
                m_window.Content is FrameworkElement root)
            {
                xamlRootTask = WaitForXamlRootAsync(root);
            }

            m_window.Activate();

            if (xamlRootTask is not null)
            {
                var dialog = new BackendApiKeyPromptDialog();
                dialog.XamlRoot = await xamlRootTask;

                ContentDialogResult result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    settingsService.BackendApiKey = dialog.ApiKey;
                    settingsService.Save();
                }
            }

            if (!string.IsNullOrWhiteSpace(settingsService.BackendApiKey))
            {
                StartBackendServices();
            }
        }

        private static Task<XamlRoot> WaitForXamlRootAsync(FrameworkElement root)
        {
            if (root.XamlRoot is XamlRoot xamlRoot)
            {
                return Task.FromResult(xamlRoot);
            }

            var completion = new TaskCompletionSource<XamlRoot>(TaskCreationOptions.RunContinuationsAsynchronously);
            RoutedEventHandler loadedHandler = null!;
            loadedHandler = (_, _) =>
            {
                root.Loaded -= loadedHandler;
                if (root.XamlRoot is XamlRoot loadedXamlRoot)
                {
                    completion.TrySetResult(loadedXamlRoot);
                    return;
                }

                completion.TrySetException(new InvalidOperationException("The main window XamlRoot is unavailable."));
            };
            root.Loaded += loadedHandler;
            return completion.Task;
        }

        private void StartBackendServices()
        {
            Services.GetRequiredService<IBackendPowerSseService>().Start();
            Services.GetRequiredService<IBackendDashboardSyncService>().Start();
            Services.GetRequiredService<IOperatingHoursAutoSyncService>().Start();
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            
            // Views
            services.AddSingleton<MainWindow>();
            services.AddTransient<DashboardPage>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<DeviceDetailPage>();
            services.AddTransient<SerialMonitorWindow>();
            services.AddTransient<ServerMonitorPage>();
            services.AddTransient<OperatingHoursSyncPage>();
            
            // Core Services
            // XBeeService must be registered before SerialService since SerialService depends on it
            services.AddSingleton<IComRawTrafficTap, ComRawTrafficTap>();
            services.AddSingleton<XBeeService>();
            services.AddSingleton<ISerialService, SerialService>();
            services.AddSingleton<ISerialTrafficTap, SerialTrafficTap>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IBackendApiKeyStore, BackendApiKeyStore>();
            services.AddSingleton<HttpClient>(_ => new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            });
            services.AddSingleton<IBackendSettingsPathProvider, BackendSettingsPathProvider>();
            services.AddSingleton<IBackendObjectIdResolver, BackendObjectIdResolver>();
            services.AddSingleton<IBackendMonitoringService, BackendMonitoringService>();
            services.AddSingleton<IBackendPowerSseService, BackendPowerSseService>();
            services.AddSingleton<IBackendServerCatalogClient, BackendServerCatalogClient>();
            services.AddSingleton<IBackendDashboardSyncService, BackendDashboardSyncService>();
            services.AddSingleton<IBackendTrafficTap, BackendTrafficTap>();
            services.AddSingleton<IOperatingHoursCache, OperatingHoursCache>();
            services.AddSingleton<IOperatingHoursSource, OperatingHoursSource>();
            services.AddSingleton<IOperatingHoursDeviceSyncService, OperatingHoursDeviceSyncService>();
            services.AddSingleton<IOperatingHoursAutoSyncService, OperatingHoursAutoSyncService>();
            services.AddSingleton<ILocalizationService, LocalizationService>();


            // ViewModels
            services.AddSingleton<DashboardViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<DeviceDetailViewModel>();
            services.AddTransient<BackendSettingsViewModel>();
            services.AddTransient<ScanDialogViewModel>();
            services.AddTransient<SerialMonitorViewModel>();
            services.AddTransient<ServerMonitorViewModel>();
            services.AddTransient<OperatingHoursSyncViewModel>();

            // Window Hosts
            services.AddSingleton<SerialMonitorWindowHost>();
            
            return services.BuildServiceProvider();
        }
    }
}
