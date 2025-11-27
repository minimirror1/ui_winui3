using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using System;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Infrastructure;
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

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = Services.GetRequiredService<MainWindow>();
            m_window.Activate();
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            
            // Views
            services.AddSingleton<MainWindow>();
            services.AddTransient<DashboardPage>();
            services.AddTransient<SettingsPage>();
            services.AddTransient<DeviceDetailPage>();
            
            // Core Services
            services.AddSingleton<ISerialService, SerialService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ILocalizationService, LocalizationService>();

            // ViewModels
            services.AddSingleton<DashboardViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<DeviceDetailViewModel>();
            services.AddTransient<ScanDialogViewModel>();
            
            return services.BuildServiceProvider();
        }
    }
}
