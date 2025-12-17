using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.UI.Views
{
    public sealed partial class SerialMonitorWindow : Window
    {
        public SerialMonitorWindow()
        {
            InitializeComponent();

            this.SystemBackdrop = new MicaBackdrop();

            var localizationService = App.Current.Services.GetRequiredService<ILocalizationService>();
            this.Title = localizationService.GetString("SerialMonitor_Title");
        }
    }
}

