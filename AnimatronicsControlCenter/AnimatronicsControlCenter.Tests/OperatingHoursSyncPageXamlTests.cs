using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class OperatingHoursSyncPageXamlTests
{
    [TestMethod]
    public void MainWindow_HasOperatingHoursSyncFooterButton()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x    = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument page  = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml"));

        XElement footerStack = page.Descendants(xaml + "NavigationView.PaneFooter").Single()
                                   .Elements(xaml + "StackPanel").Single();
        var buttons = footerStack.Elements(xaml + "Button").ToList();

        Assert.AreEqual("BackendSettingsButton",     (string?)buttons[^2].Attribute(x + "Name"));
        Assert.AreEqual("OperatingHoursSyncButton",  (string?)buttons[^1].Attribute(x + "Name"));
        Assert.AreEqual("OperatingHoursSyncButton_Click", (string?)buttons[^1].Attribute("Click"));
    }

    [TestMethod]
    public void OperatingHoursSyncPage_HasServerAndDevicePaneCommands()
    {
        string text = LoadPageText();

        // Server pane
        StringAssert.Contains(text, "LoadFromServerCommand");
        StringAssert.Contains(text, "PushToServerCommand");
        StringAssert.Contains(text, "ServerDays");

        // Device pane
        StringAssert.Contains(text, "DeviceDays");
        StringAssert.Contains(text, "DeviceRangeFrom");
        StringAssert.Contains(text, "DeviceRangeTo");
        StringAssert.Contains(text, "DeviceNavText");        // formatted CurrentDeviceId
        StringAssert.Contains(text, "NavigatePrevCommand");
        StringAssert.Contains(text, "NavigateNextCommand");
    }

    [TestMethod]
    public void OperatingHoursSyncPage_HasAllFooterButtons()
    {
        string text = LoadPageText();

        StringAssert.Contains(text, "CompareCurrentDeviceCommand");
        StringAssert.Contains(text, "SendToCurrentDeviceCommand");
        StringAssert.Contains(text, "CompareAllDevicesCommand");
        StringAssert.Contains(text, "SendToAllDevicesCommand");
    }

    [TestMethod]
    public void OperatingHoursSyncPage_HasBatchProgressElements()
    {
        string text = LoadPageText();

        StringAssert.Contains(text, "IsBatchInProgress");
        StringAssert.Contains(text, "BatchProgressValue");
        StringAssert.Contains(text, "BatchLabel");
    }

    [TestMethod]
    public void OperatingHoursSyncPage_UsesItemsRepeaterForSchedules()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XDocument page  = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "OperatingHoursSyncPage.xaml"));

        bool hasServerRepeater = page.Descendants(xaml + "ItemsRepeater")
            .Any(el => ((string?)el.Attribute("ItemsSource"))?.Contains("ServerDays") == true);
        bool hasDeviceRepeater = page.Descendants(xaml + "ItemsRepeater")
            .Any(el => ((string?)el.Attribute("ItemsSource"))?.Contains("DeviceDays") == true);

        Assert.IsTrue(hasServerRepeater, "ServerDays에 바인딩된 ItemsRepeater가 없습니다.");
        Assert.IsTrue(hasDeviceRepeater, "DeviceDays에 바인딩된 ItemsRepeater가 없습니다.");
    }

    [TestMethod]
    public void OperatingHoursSyncPage_DoesNotReferenceOldProperties()
    {
        string text = LoadPageText();

        // Old VM properties that no longer exist
        Assert.IsFalse(text.Contains("ScheduleDays",    StringComparison.Ordinal), "ScheduleDays 참조가 남아 있습니다.");
        Assert.IsFalse(text.Contains("SyncCommand",     StringComparison.Ordinal), "SyncCommand 참조가 남아 있습니다.");
        Assert.IsFalse(text.Contains("TimeRangeText",   StringComparison.Ordinal), "TimeRangeText 참조가 남아 있습니다.");
        Assert.IsFalse(text.Contains("StartDeviceId",   StringComparison.Ordinal), "StartDeviceId 참조가 남아 있습니다.");
        Assert.IsFalse(text.Contains("EndDeviceId",     StringComparison.Ordinal), "EndDeviceId 참조가 남아 있습니다.");
    }

    // ─────────────────────────── Helper ──────────────────────────────────────────

    private static string LoadPageText()
        => File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "OperatingHoursSyncPage.xaml"));

    private static string ProjectPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine([directory.FullName, ..segments]);
            if (File.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }

        Assert.Fail($"프로젝트 파일을 찾을 수 없습니다: {Path.Combine(segments)}");
        return string.Empty;
    }
}
