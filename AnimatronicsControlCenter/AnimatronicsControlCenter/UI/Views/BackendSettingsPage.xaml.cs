using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
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
}
