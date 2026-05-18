using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class ThemeModeSupportTests
{
    [TestMethod]
    public void Windows_ExposeThemeRootsAndApplyRequestedTheme()
    {
        string mainWindowXaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml"));
        string mainWindowCode = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml.cs"));
        string serialWindowXaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SerialMonitorWindow.xaml"));
        string serialWindowCode = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SerialMonitorWindow.xaml.cs"));

        StringAssert.Contains(mainWindowXaml, "x:Name=\"RootGrid\"");
        StringAssert.Contains(mainWindowCode, "ApplyTheme");
        StringAssert.Contains(mainWindowCode, "RequestedTheme");
        StringAssert.Contains(serialWindowXaml, "x:Name=\"RootGrid\"");
        StringAssert.Contains(serialWindowCode, "ApplyTheme");
        StringAssert.Contains(serialWindowCode, "RequestedTheme");
    }

    [TestMethod]
    public void Dashboard_UsesThemeDictionariesForForcedCardColors()
    {
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DashboardPage.xaml"));
        string xaml = page.ToString(SaveOptions.DisableFormatting);

        StringAssert.Contains(xaml, "ResourceDictionary.ThemeDictionaries");
        StringAssert.Contains(xaml, "x:Key=\"Light\"");
        StringAssert.Contains(xaml, "x:Key=\"Dark\"");
        StringAssert.Contains(xaml, "DashboardDeviceCardBackgroundBrush");
        StringAssert.Contains(xaml, "DashboardPlayButtonBackgroundBrush");
    }

    [TestMethod]
    public void Dashboard_LightCardHoverColorIsVisiblyDifferentFromBaseColor()
    {
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DashboardPage.xaml"));
        (int r, int g, int b) baseColor = GetThemeBrushColor(page, "Light", "DashboardDeviceCardBackgroundBrush");
        (int r, int g, int b) hoverColor = GetThemeBrushColor(page, "Light", "DashboardDeviceCardHoverBackgroundBrush");

        Assert.IsTrue(
            ColorDistance(baseColor, hoverColor) >= 40,
            "Light mode device card hover color should be visibly different from the white base card.");
    }

    [TestMethod]
    public void Dashboard_HoverCodeUsesThemeAwareBrushLookup()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DashboardPage.xaml.cs"));

        StringAssert.Contains(code, "GetDashboardBrush");
        Assert.IsFalse(
            code.Contains("Resources[\"DashboardDeviceCardHoverBackgroundBrush\"]", StringComparison.Ordinal),
            "Hover should not read theme dictionary brushes through the page resource indexer directly.");
    }

    [TestMethod]
    public void DirectDarkOnlyHexColors_AreRemovedFromThemeableSurfaces()
    {
        string mainWindow = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml"));
        string slideControl = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Controls", "SlideToUnlockControl.xaml"));

        Assert.IsFalse(mainWindow.Contains("Background=\"#10FFFFFF\"", StringComparison.Ordinal));
        Assert.IsFalse(slideControl.Contains("Background=\"#2a2a2a\"", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(slideControl.Contains("BorderBrush=\"#1a1a1a\"", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(slideControl.Contains("Foreground=\"#8a8a8a\"", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void DeviceDetailRelayControls_UseThemeResourcesForTextAndPowerButtons()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DeviceDetailPage.xaml"));

        StringAssert.Contains(xaml, "ResourceDictionary.ThemeDictionaries");
        StringAssert.Contains(xaml, "RelayControlPrimaryTextBrush");
        StringAssert.Contains(xaml, "RelayControlSecondaryTextBrush");
        StringAssert.Contains(xaml, "RelayControlUnlockedTextBrush");
        StringAssert.Contains(xaml, "RelayPowerOffButtonBackgroundBrush");
        StringAssert.Contains(xaml, "RelayPowerOnButtonBackgroundBrush");
        StringAssert.Contains(xaml, "RelayPowerOffButtonStyle");
        StringAssert.Contains(xaml, "RelayPowerOnButtonStyle");
        StringAssert.Contains(xaml, "Style=\"{StaticResource RelayPowerOffButtonStyle}\"");
        StringAssert.Contains(xaml, "Style=\"{StaticResource RelayPowerOnButtonStyle}\"");
    }

    [TestMethod]
    public void DeviceDetailRelayPowerButtons_OverridePointerStateColors()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "DeviceDetailPage.xaml"));

        Assert.IsTrue(CountOccurrences(xaml, "ButtonBackgroundPointerOver") >= 2);
        Assert.IsTrue(CountOccurrences(xaml, "ButtonBackgroundPressed") >= 2);
        Assert.IsTrue(CountOccurrences(xaml, "ButtonForegroundPointerOver") >= 2);
        Assert.IsTrue(CountOccurrences(xaml, "ButtonBorderBrushPointerOver") >= 2);
    }

    [TestMethod]
    public void SlideToUnlockControl_CodeResolvesBrushesFromActualThemeDictionary()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Controls", "SlideToUnlockControl.xaml.cs"));

        StringAssert.Contains(code, "ResolveThemeBrush");
        StringAssert.Contains(code, "ThemeDictionaries");
        Assert.IsFalse(
            code.Contains("=> (SolidColorBrush)Resources[resourceKey]", StringComparison.Ordinal),
            "Slide state changes should not bypass theme dictionary selection.");
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

    private static (int r, int g, int b) GetThemeBrushColor(XDocument page, string theme, string key)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement themeDictionary = page
            .Descendants(xaml + "ResourceDictionary")
            .Single(element =>
                (string?)element.Attribute(x + "Key") == theme &&
                element.Elements(xaml + "SolidColorBrush").Any(brush => (string?)brush.Attribute(x + "Key") == key));
        XElement brush = themeDictionary
            .Elements(xaml + "SolidColorBrush")
            .Single(element => (string?)element.Attribute(x + "Key") == key);

        string color = ((string?)brush.Attribute("Color") ?? string.Empty).TrimStart('#');
        Assert.AreEqual(6, color.Length, $"{key} should use #RRGGBB format.");
        return (
            Convert.ToInt32(color[..2], 16),
            Convert.ToInt32(color[2..4], 16),
            Convert.ToInt32(color[4..6], 16));
    }

    private static int ColorDistance((int r, int g, int b) first, (int r, int g, int b) second)
        => Math.Abs(first.r - second.r) + Math.Abs(first.g - second.g) + Math.Abs(first.b - second.b);

    private static int CountOccurrences(string value, string search)
    {
        int count = 0;
        int index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }
}
