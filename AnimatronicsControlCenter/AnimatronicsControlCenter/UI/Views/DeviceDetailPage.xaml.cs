using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.UI.Xaml.Input;
using AnimatronicsControlCenter.Core.Models;
using Microsoft.UI.Xaml.Navigation;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class DeviceDetailPage : Page
    {
        public DeviceDetailViewModel ViewModel { get; }

        public DeviceDetailPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<DeviceDetailViewModel>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is Device device)
            {
                ViewModel.SelectedDevice = device;
            }
        }

        private void Slider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Slider slider && slider.DataContext is MotorState motor)
            {
                ViewModel.MoveMotorCommand.Execute(motor);
            }
        }
    }
}
