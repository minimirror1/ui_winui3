using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class BackendRegistrationDialog : ContentDialog
{
    public BackendRegistrationViewModel ViewModel { get; }

    public BackendRegistrationDialog(BackendRegistrationViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        ViewModel.RequestClose += () => Hide();
    }

    private Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;
}
