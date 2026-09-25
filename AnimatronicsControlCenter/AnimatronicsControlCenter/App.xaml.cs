using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Utilities;
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

            // 앱이 아무 흔적 없이 종료되면 무엇이 터졌는지 알 길이 없다. 파일로 남긴다.
            UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                string folder = LogFilePolicy.FolderFor(
                    Services.GetRequiredService<ISettingsService>().AppSettingsFilePath);
                System.IO.Directory.CreateDirectory(folder);

                string path = System.IO.Path.Combine(
                    folder, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

                var text = new System.Text.StringBuilder();
                text.AppendLine($"시각: {DateTimeOffset.Now:O}");
                text.AppendLine($"메시지: {e.Message}");
                text.AppendLine();

                for (Exception? ex = e.Exception; ex != null; ex = ex.InnerException)
                {
                    text.AppendLine($"--- {ex.GetType().FullName} ---");
                    text.AppendLine($"HResult: 0x{ex.HResult:X8}");
                    text.AppendLine(ex.Message);
                    text.AppendLine(ex.StackTrace);
                    text.AppendLine();
                }

                System.IO.File.WriteAllText(path, text.ToString(), new System.Text.UTF8Encoding(true));
            }
            catch
            {
                // 기록에 실패해도 원래 예외를 가리지 않는다.
            }
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();
            settingsService.Load();
            
            StartTrafficLog(settingsService);

            // 인터넷 시간 동기화: 즉시 1회 + 1시간 주기 재동기화 (실패 시 PC 시계 폴백)
            Services.GetRequiredService<INetworkTimeService>().Start(TimeSpan.FromHours(1));


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

        /// 오래된 기록을 먼저 치우고, 설정대로 기록을 켠다.
        /// 구독은 시작 직후에 걸어야 그 사이에 난 문제도 남는다.
        private void StartTrafficLog(ISettingsService settingsService)
        {
            string folder = LogFilePolicy.FolderFor(settingsService.AppSettingsFilePath);
            LogFileCleaner.Clean(
                folder,
                DateOnly.FromDateTime(DateTime.Now),
                LogFilePolicy.DefaultRetentionDays,
                LogFilePolicy.DefaultMaxTotalBytes);

            var logger = Services.GetRequiredService<TrafficFileLogger>();
            logger.IsEnabled = settingsService.IsTrafficLogEnabled;
            logger.IncludeRaw = settingsService.IsTrafficLogRawIncluded;
            logger.Subscribe(Services.GetRequiredService<ISerialTrafficTap>());
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
            services.AddTransient<ReleaseNotesWindow>();
            services.AddTransient<ServerMonitorPage>();
            services.AddTransient<OperatingHoursSyncPage>();
            services.AddTransient<LogViewerPage>();
            
            // Core Services
            // XBeeService must be registered before SerialService since SerialService depends on it
            services.AddSingleton<IComRawTrafficTap, ComRawTrafficTap>();
            services.AddSingleton<XBeeService>();
            services.AddSingleton<ISerialService, SerialService>();
            services.AddSingleton<ISerialTrafficTap, SerialTrafficTap>();
            // 통신 기록 파일. 설정 파일 옆 Logs 폴더에 날짜별로 쌓는다.
            services.AddSingleton<TrafficFileLogger>(provider => new TrafficFileLogger(
                LogFilePolicy.FolderFor(provider.GetRequiredService<ISettingsService>().AppSettingsFilePath),
                new TrafficFileLoggerOptions(
                    LogFilePolicy.DefaultMaxFileBytes,
                    QueueCapacity: 10_000,
                    IncludeRaw: false)));
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<INetworkTimeService, NetworkTimeService>();
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
            services.AddTransient<ReleaseNotesViewModel>();
            services.AddTransient<LogViewerViewModel>();

            // Window Hosts
            services.AddSingleton<SerialMonitorWindowHost>();
            services.AddSingleton<ReleaseNotesWindowHost>();
            
            return services.BuildServiceProvider();
        }
    }
}
