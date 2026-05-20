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
    public void OperatingHoursSyncPage_UsesDashboardStyleTwoPaneLayout()
    {
        string text = LoadPageText();

        StringAssert.Contains(text, "x:Name=\"OperatingHoursHeader\"");
        StringAssert.Contains(text, "운영시간 관리");
        StringAssert.Contains(text, "서버 마스터 스케줄");
        StringAssert.Contains(text, "장치 저장 스케줄");
        StringAssert.Contains(text, "x:Name=\"ServerSchedulePane\"");
        StringAssert.Contains(text, "x:Name=\"DeviceSchedulePane\"");
        StringAssert.Contains(text, "x:Name=\"DeviceActionBar\"");
        StringAssert.Contains(text, "OpsServerPanelBorderBrush");
        StringAssert.Contains(text, "OpsDevicePanelBorderBrush");
        StringAssert.Contains(text, "OpsScheduleRowBrush");
    }

    [TestMethod]
    public void OperatingHoursSyncPage_ConstrainsLongTextAndFooterActions()
    {
        string text = LoadPageText();

        StringAssert.Contains(text, "x:Name=\"AutoSyncBadge\"");
        StringAssert.Contains(text, "MinWidth=\"150\"");
        StringAssert.Contains(text, "x:Name=\"ServerMetaRow\"");
        StringAssert.Contains(text, "x:Name=\"ServerStatusPillText\"");
        StringAssert.Contains(text, "MaxWidth=\"150\"");
        StringAssert.Contains(text, "x:Name=\"DeviceNavBar\"");
        StringAssert.Contains(text, "x:Name=\"DeviceActionsGrid\"");
        StringAssert.Contains(text, "x:Name=\"DeviceSendActionsGroup\"");
        StringAssert.Contains(text, "x:Name=\"DeviceCompareActionsGroup\"");
        StringAssert.Contains(text, "<ColumnDefinition Width=\"*\"/>");
        StringAssert.Contains(text, "MinWidth=\"118\"");
        StringAssert.Contains(text, "HorizontalAlignment=\"Stretch\"");
        StringAssert.Contains(text, "TextTrimming=\"CharacterEllipsis\"");
    }

    [TestMethod]
    public void OperatingHoursSyncPage_ServerRowsShowHolidayToggleAndClosedPlaceholder()
    {
        string text = LoadPageText();

        StringAssert.Contains(text, "x:Name=\"ServerScheduleRowGrid\"");
        StringAssert.Contains(text, "x:Name=\"ServerClosedPlaceholder\"");
        StringAssert.Contains(text, "BorderThickness=\"1\"");
        StringAssert.Contains(text, "BorderBrush=\"{ThemeResource ControlStrokeColorDefaultBrush}\"");
        StringAssert.Contains(text, "Text=\"휴무일\"");
        StringAssert.Contains(text, "x:Name=\"ServerHolidayToggle\"");
        StringAssert.Contains(text, "IsChecked=\"{x:Bind IsClosed, Mode=TwoWay}\"");
        StringAssert.Contains(text, "x:Name=\"ServerHolidayToggleTrack\"");
        StringAssert.Contains(text, "x:Name=\"ServerHolidayToggleOnTrack\"");
        StringAssert.Contains(text, "x:Name=\"ServerHolidayToggleThumbOff\"");
        StringAssert.Contains(text, "x:Name=\"ServerHolidayToggleThumbOn\"");
        StringAssert.Contains(text, "Visibility=\"{x:Bind IsClosed, Mode=OneWay, Converter={StaticResource BoolToVisibility}}\"");
        StringAssert.Contains(text, "Visibility=\"{x:Bind IsClosed, Mode=OneWay, Converter={StaticResource InverseBoolToVisibility}}\"");
        StringAssert.Contains(text, "Background=\"{ThemeResource ControlFillColorDefaultBrush}\"");
        StringAssert.Contains(text, "Background=\"{ThemeResource OpsServerAccentBrush}\"");
        StringAssert.Contains(text, "Width=\"42\"");
        StringAssert.Contains(text, "HorizontalAlignment=\"Right\"");
    }

    [TestMethod]
    public void OperatingHoursSyncPage_ServerTimePickersKeepRoundedEdgesVisible()
    {
        string text = LoadPageText();

        StringAssert.Contains(text, "x:Name=\"ServerOpenTimePicker\"");
        StringAssert.Contains(text, "x:Name=\"ServerCloseTimePicker\"");
        StringAssert.Contains(text, "ColumnSpacing=\"8\"");
        StringAssert.Contains(text, "<ColumnDefinition Width=\"56\"/>");
        StringAssert.Contains(text, "<ColumnDefinition Width=\"42\"/>");
        StringAssert.Contains(text, "HorizontalAlignment=\"Stretch\"");
        Assert.IsFalse(text.Contains("ServerOpenTimeButton", StringComparison.Ordinal), "TimePicker를 커스텀 버튼으로 감싸면 안 됩니다.");
        Assert.IsFalse(text.Contains("ServerCloseTimeButton", StringComparison.Ordinal), "TimePicker를 커스텀 버튼으로 감싸면 안 됩니다.");
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

    [TestMethod]
    public void OperatingHoursSyncPage_ServerHeaderSeparatesStoreInfoAndActions()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "OperatingHoursSyncPage.xaml"));

        XElement header = page
            .Descendants(xaml + "Grid")
            .Single(element => (string?)element.Attribute(x + "Name") == "ServerScheduleHeader");

        Assert.AreEqual(2, header.Element(xaml + "Grid.ColumnDefinitions")?.Elements(xaml + "ColumnDefinition").Count());

        XElement storeInfo = header
            .Descendants(xaml + "TextBlock")
            .Single(element => ((string?)element.Attribute("Text"))?.Contains("StoreInfoText") == true);

        Assert.AreEqual("0", (string?)storeInfo.Parent?.Attribute("Grid.Column"));
        Assert.AreEqual("CharacterEllipsis", (string?)storeInfo.Attribute("TextTrimming"));

        XElement actions = header
            .Descendants(xaml + "StackPanel")
            .Single(element => (string?)element.Attribute("Orientation") == "Horizontal"
                && element.Descendants(xaml + "Button").Any(button => ((string?)button.Attribute("Command"))?.Contains("LoadFromServerCommand") == true));

        Assert.AreEqual("1", (string?)actions.Attribute("Grid.Column"));
    }

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
