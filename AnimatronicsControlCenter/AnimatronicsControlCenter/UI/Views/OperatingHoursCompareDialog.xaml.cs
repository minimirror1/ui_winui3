using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace AnimatronicsControlCenter.UI.Views;

public sealed partial class OperatingHoursCompareDialog : ContentDialog
{
    public OperatingHoursSyncViewModel Vm { get; }

    public OperatingHoursCompareDialog(OperatingHoursSyncViewModel vm)
    {
        Vm = vm;
        InitializeComponent();
    }
}
