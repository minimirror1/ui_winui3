using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class MainWindowLayoutTests
{
    [TestMethod]
    public void SerialTrafficIndicator_IsCenteredWithinCompactPaneFooter()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml"));

        XElement? footerHost = page
            .Descendants(xaml + "Grid")
            .SingleOrDefault(element => (string?)element.Attribute(x + "Name") == "SerialTrafficFooterHost");

        Assert.IsNotNull(footerHost, "Serial traffic footer should have a fixed compact-pane host.");
        Assert.AreEqual("48", (string?)footerHost.Attribute("Width"));
        Assert.AreEqual("Left", (string?)footerHost.Attribute("HorizontalAlignment"));

        XElement button = footerHost
            .Descendants(xaml + "Button")
            .Single(element => (string?)element.Attribute(x + "Name") == "SerialTrafficButton");

        Assert.AreEqual("Center", (string?)button.Attribute("HorizontalAlignment"));
    }

    [TestMethod]
    public void BackendSettingsButton_IsLastPaneFooterButtonAboveSettingsItem()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml"));

        XElement footerStack = page
            .Descendants(xaml + "NavigationView.PaneFooter")
            .Single()
            .Elements(xaml + "StackPanel")
            .Single();

        XElement lastButton = footerStack
            .Elements()
            .Where(element => element.Name == xaml + "Button")
            .Last();

        Assert.AreEqual("BackendSettingsButton", (string?)lastButton.Attribute(x + "Name"));
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
