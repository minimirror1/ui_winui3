using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class DeviceDetailPageXamlTests
{
    [TestMethod]
    public void DeviceDetailViewModel_ExposesRepeatPlayMotionCommand()
    {
        string source = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "ViewModels", "DeviceDetailViewModel.cs"));

        StringAssert.Contains(source, "private async Task RepeatPlayMotionAsync()");
        StringAssert.Contains(source, "BinaryMotionAction.RepeatPlay");
        StringAssert.Contains(source, "RefreshDeviceStatusForDeviceAsync(SelectedDevice, CancellationToken.None)");
    }

    [TestMethod]
    public void DeviceDetailViewModel_ExposesClearErrorCommand()
    {
        string source = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "ViewModels", "DeviceDetailViewModel.cs"));

        StringAssert.Contains(source, "private async Task ClearErrorAsync()");
        StringAssert.Contains(source, "BinarySerializer.EncodeErrorClear");
        StringAssert.Contains(source, "ClearErrorCommand.NotifyCanExecuteChanged()");
        StringAssert.Contains(source, "RefreshDeviceStatusForDeviceAsync(SelectedDevice, CancellationToken.None)");
    }

    [TestMethod]
    public void DashboardViewModel_PollingCopiesErrorStatusWithPowerStatus()
    {
        string source = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "ViewModels", "DashboardViewModel.cs"));

        StringAssert.Contains(source, "target.PowerStatus = source.PowerStatus;");
        StringAssert.Contains(source, "target.HasError = source.HasError;");
        StringAssert.Contains(source, "target.HasError = false;");
    }

    [TestMethod]
    public void DeviceDetailPage_AddsSecondaryRepeatPlayButton()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DeviceDetailPage.xaml"));

        StringAssert.Contains(xaml, "RepeatPlayMotionCommand");
        StringAssert.Contains(xaml, "ToolTipService.ToolTip=\"Repeat Play\"");
        StringAssert.Contains(xaml, "x:Name=\"RepeatPlayGeneratedIcon\"");
        StringAssert.Contains(xaml, "x:Name=\"RepeatPlayTopArrowPath\"");
        StringAssert.Contains(xaml, "x:Name=\"RepeatPlayBottomArrowPath\"");
        StringAssert.Contains(xaml, "x:Name=\"RepeatPlayTrianglePath\"");
        StringAssert.Contains(xaml, "Data=\"M8,5.6 L16,11 L8,16.4 Z\"");
        StringAssert.Contains(xaml, "Fill=\"{ThemeResource TextFillColorPrimaryBrush}\"");
        Assert.IsFalse(xaml.Contains("Glyph=\"&#xE8EE;\"", StringComparison.Ordinal), "RepeatPlay should use a composite play/repeat icon, not the plain repeat glyph.");
        Assert.IsFalse(xaml.Contains("Source=\"/Assets/RepeatPlay.png\"", StringComparison.Ordinal), "RepeatPlay should use a generated vector icon, not the reference image asset.");
        Assert.IsFalse(xaml.Contains("x:Name=\"RepeatPlayImageIcon\"", StringComparison.Ordinal), "RepeatPlay should use a generated vector icon, not an Image control.");
    }

    [TestMethod]
    public void DeviceDetailPage_PositionsStopAndPauseBetweenPlayAndRepeatPlayWithoutSeparatingPlayAndRepeat()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DeviceDetailPage.xaml"));

        StringAssert.Contains(xaml, "x:Name=\"MotionControlButtonGrid\"");
        StringAssert.Contains(xaml, "x:Name=\"PlayMotionButton\"");
        StringAssert.Contains(xaml, "x:Name=\"StopMotionButton\"");
        StringAssert.Contains(xaml, "x:Name=\"PauseMotionButton\"");
        StringAssert.Contains(xaml, "x:Name=\"RepeatPlayMotionButton\"");

        StringAssert.Contains(xaml, "x:Name=\"PlayMotionButton\" Grid.Row=\"0\" Grid.Column=\"1\"");
        StringAssert.Contains(xaml, "x:Name=\"RepeatPlayMotionButton\" Grid.Row=\"1\" Grid.Column=\"1\"");
        StringAssert.Contains(xaml, "x:Name=\"StopMotionButton\" Grid.Row=\"0\" Grid.RowSpan=\"2\" Grid.Column=\"0\"");
        StringAssert.Contains(xaml, "x:Name=\"PauseMotionButton\" Grid.Row=\"0\" Grid.RowSpan=\"2\" Grid.Column=\"2\"");
        StringAssert.Contains(xaml, "VerticalAlignment=\"Center\"");
    }

    [TestMethod]
    public void DeviceDetailPage_PlacesPowerTabBetweenOverviewAndFiles()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DeviceDetailPage.xaml"));

        int overviewIndex = xaml.IndexOf("DeviceDetail_Tab_Overview", StringComparison.Ordinal);
        int powerIndex = xaml.IndexOf("DeviceDetail_Tab_Power", StringComparison.Ordinal);
        int filesIndex = xaml.IndexOf("DeviceDetail_Tab_Files", StringComparison.Ordinal);

        Assert.IsTrue(overviewIndex >= 0, "Overview tab should exist.");
        Assert.IsTrue(powerIndex >= 0, "Power tab should exist.");
        Assert.IsTrue(filesIndex >= 0, "Files tab should exist.");
        Assert.IsTrue(overviewIndex < powerIndex && powerIndex < filesIndex, "Power tab should be between overview and files.");
    }

    [TestMethod]
    public void DeviceDetailPage_MovesRelayControlsFromOverviewToPowerTab()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DeviceDetailPage.xaml"));

        int overviewIndex = xaml.IndexOf("DeviceDetail_Tab_Overview", StringComparison.Ordinal);
        int powerIndex = xaml.IndexOf("DeviceDetail_Tab_Power", StringComparison.Ordinal);
        int filesIndex = xaml.IndexOf("DeviceDetail_Tab_Files", StringComparison.Ordinal);

        Assert.IsTrue(overviewIndex >= 0, "Overview tab should exist.");
        Assert.IsTrue(powerIndex >= 0, "Power tab should exist.");
        Assert.IsTrue(filesIndex >= 0, "Files tab should exist.");

        string overviewTab = xaml[overviewIndex..powerIndex];
        string powerTab = xaml[powerIndex..filesIndex];

        foreach (string relayToken in new[]
        {
            "DeviceDetail_RelayControl",
            "SlideToUnlock_Unlocked",
            "SetPowerOffCommand",
            "SetPowerOnCommand",
            "DeviceDetail_RelayWarning",
        })
        {
            Assert.IsFalse(overviewTab.Contains(relayToken, StringComparison.Ordinal), $"{relayToken} should not remain in overview tab.");
            StringAssert.Contains(powerTab, relayToken);
        }
    }

    [TestMethod]
    public void DeviceDetailPage_RelayWarningTextHasConstrainedColumnForWrapping()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DeviceDetailPage.xaml"));

        int warningIndex = xaml.IndexOf("DeviceDetail_RelayWarning", StringComparison.Ordinal);

        Assert.IsTrue(warningIndex >= 0, "Relay warning text should exist.");

        string warningLayout = xaml[Math.Max(0, warningIndex - 1200)..warningIndex];

        StringAssert.Contains(warningLayout, "<Grid.ColumnDefinitions>");
        StringAssert.Contains(warningLayout, "<ColumnDefinition Width=\"Auto\"/>");
        StringAssert.Contains(warningLayout, "<ColumnDefinition Width=\"*\"/>");
        StringAssert.Contains(xaml[Math.Max(0, warningIndex - 160)..Math.Min(xaml.Length, warningIndex + 240)], "Grid.Column=\"1\"");
    }

    [TestMethod]
    public void DeviceDetailPage_AddsErrorStatusPanelBetweenPowerButtonsAndWarning()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DeviceDetailPage.xaml"));

        int powerOffIndex = xaml.IndexOf("SetPowerOffCommand", StringComparison.Ordinal);
        int errorPanelIndex = xaml.IndexOf("DeviceDetail_ErrorStatus", StringComparison.Ordinal);
        int clearErrorIndex = xaml.IndexOf("ClearErrorCommand", StringComparison.Ordinal);
        int warningIndex = xaml.IndexOf("DeviceDetail_RelayWarning", StringComparison.Ordinal);

        Assert.IsTrue(powerOffIndex >= 0, "Power controls should exist.");
        Assert.IsTrue(errorPanelIndex >= 0, "Error status panel should exist.");
        Assert.IsTrue(clearErrorIndex >= 0, "Error clear button should exist.");
        Assert.IsTrue(warningIndex >= 0, "Relay warning should exist.");
        Assert.IsTrue(powerOffIndex < errorPanelIndex && errorPanelIndex < clearErrorIndex && clearErrorIndex < warningIndex,
            "Error status panel should be placed between the power buttons and relay warning.");
    }

    private static string ProjectPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        Assert.Fail($"Could not find project file: {Path.Combine(segments)}");
        return string.Empty;
    }

}
