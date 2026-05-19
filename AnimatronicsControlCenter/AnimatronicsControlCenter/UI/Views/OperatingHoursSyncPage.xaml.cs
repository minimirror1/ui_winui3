using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class OperatingHoursSyncPage : Page
{
    public OperatingHoursSyncPage()
    {
        InitializeComponent();
        ViewModel = App.Current.Services.GetRequiredService<OperatingHoursSyncViewModel>();
    }

    public OperatingHoursSyncViewModel ViewModel { get; }
}
