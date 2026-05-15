using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class DeviceDetailPageXamlTests
{
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
