using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class BackendApiKeyPromptDialog : ContentDialog
{
    public BackendApiKeyPromptViewModel ViewModel { get; } = new();

    public string ApiKey => ViewModel.ApiKeyToSave;

    public BackendApiKeyPromptDialog()
    {
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApiKeyPasswordBox.Focus(FocusState.Programmatic);
    }
}
