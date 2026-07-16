using CommunityToolkit.Mvvm.ComponentModel;

namespace AnimatronicsControlCenter.UI.ViewModels;

public partial class BackendApiKeyPromptViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(ApiKeyToSave))]
    private string apiKey = string.Empty;

    public bool CanSave => !string.IsNullOrWhiteSpace(ApiKey);

    public string ApiKeyToSave => ApiKey.Trim();
}
