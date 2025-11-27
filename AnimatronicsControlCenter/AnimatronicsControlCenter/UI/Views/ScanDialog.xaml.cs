using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.UI.ViewModels;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class ScanDialog : ContentDialog
    {
        public ScanDialogViewModel ViewModel { get; }

        public ScanDialog()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<ScanDialogViewModel>();
            
            ViewModel.RequestClose += () => 
            {
                // Dispatch to UI thread if needed, but RequestClose usually comes from async void command on UI thread
                Hide();
            };
        }
    }
}

