using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class OperatingHoursSyncPageXamlTests
{
    [TestMethod]
    public void MainWindow_HasBackendSettingsButtonAboveOperatingHoursFooterButton()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml"));

        XElement footerStack = page.Descendants(xaml + "NavigationView.PaneFooter").Single().Elements(xaml + "StackPanel").Single();
        var buttons = footerStack.Elements(xaml + "Button").ToList();

        Assert.AreEqual("BackendSettingsButton", (string?)buttons[^2].Attribute(x + "Name"));
        Assert.AreEqual("OperatingHoursSyncButton", (string?)buttons[^1].Attribute(x + "Name"));
        Assert.AreEqual("OperatingHoursSyncButton_Click", (string?)buttons[^1].Attribute("Click"));
    }

    [TestMethod]
    public void OperatingHoursSyncPage_HasCoreCommandsAndRangeInputs()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "OperatingHoursSyncPage.xaml"));
        string text = page.ToString();

        StringAssert.Contains(text, "LoadScheduleCommand");
        StringAssert.Contains(text, "SyncCommand");
        StringAssert.Contains(text, "ReadAndCompareCommand");
        StringAssert.Contains(text, "StartDeviceId");
        StringAssert.Contains(text, "EndDeviceId");
        StringAssert.Contains(text, "ScheduleDays");
        StringAssert.Contains(text, "OpenText");
        StringAssert.Contains(text, "CloseText");
        Assert.IsFalse(text.Contains("TimeRangeText", StringComparison.Ordinal));
        Assert.IsFalse(text.Contains(">Range<", StringComparison.Ordinal));
        Assert.IsTrue(page.Descendants(xaml + "ListView")
            .Any(element => ((string?)element.Attribute("ItemsSource"))?.Contains("ScheduleDays") == true));
        Assert.IsFalse(page.Descendants(xaml + "GridView")
            .Any(element => ((string?)element.Attribute("ItemsSource"))?.Contains("ScheduleDays") == true));
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
