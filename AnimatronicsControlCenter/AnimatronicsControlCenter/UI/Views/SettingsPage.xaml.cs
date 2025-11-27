using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.UI.ViewModels;

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
    }
}

