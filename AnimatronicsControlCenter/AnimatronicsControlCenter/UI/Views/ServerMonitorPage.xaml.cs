using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class ServerMonitorPage : Page
{
    private readonly IBackendTrafficTap _trafficTap;

    public ServerMonitorPage()
    {
        _trafficTap = App.Current.Services.GetRequiredService<IBackendTrafficTap>();
        ViewModel = App.Current.Services.GetRequiredService<ServerMonitorViewModel>();

        InitializeComponent();
        ViewModel.Refresh(System.DateTimeOffset.Now);
        _trafficTap.TrafficChanged += TrafficTap_TrafficChanged;
        Unloaded += ServerMonitorPage_Unloaded;
    }

    public ServerMonitorViewModel ViewModel { get; }

    private void TrafficTap_TrafficChanged(object? sender, System.EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => ViewModel.Refresh(System.DateTimeOffset.Now));
    }

    private void ServerMonitorPage_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        _trafficTap.TrafficChanged -= TrafficTap_TrafficChanged;
        Unloaded -= ServerMonitorPage_Unloaded;
    }

    private void CopyAllTrafficButton_Click(object sender, RoutedEventArgs e)
    {
        CopyText(ViewModel.CopyAllTrafficEntries());
    }

    private void CopySelectedTrafficButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedEntries = TrafficList.SelectedItems.OfType<ServerTrafficEntryViewModel>();
        CopyText(ServerMonitorViewModel.FormatTrafficEntries(selectedEntries));
    }

    private static void CopyText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }
}
