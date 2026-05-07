using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class BackendObjectMappingDialog : ContentDialog
{
    public BackendObjectMappingEditorViewModel ViewModel { get; }

    public BackendObjectMappingDialog(BackendObjectMappingEditorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (!ViewModel.TryBuildMappings(out _))
        {
            args.Cancel = true;
        }
    }

    private Visibility BoolToVisibility(bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;
}
