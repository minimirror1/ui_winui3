using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.UI.Xaml;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();
        }

        private Visibility BoolToVisibility(bool isVisible) => isVisible ? Visibility.Visible : Visibility.Collapsed;

        private void GridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Core.Models.Device device)
            {
                Frame.Navigate(typeof(DeviceDetailPage), device);
            }
        }
    }
}
