using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class SettingsPageReorganizationTests
{
    private static readonly string[] RequiredResourceKeys =
    [
        "Section_Connection.Text",
        "Section_Communication.Text",
        "Section_App.Text",
        "OpenSettingsFolder_Button",
        "OpenSettingsFile_Button",
        "Theme_Header.Header",
        "Theme_Desc.Text",
        "Theme_Default",
        "Theme_Light",
        "Theme_Dark",
        "XBee_Header.Header",
        "XBee_Desc.Text",
        "XBeePort_Header.Header",
        "LastPortAutoConnect_Header.Header",
        "LastPortAutoConnect_Desc.Text",
        "ResponseTimeout_Header.Header",
        "ResponseTimeout_Desc.Text",
        "PingSettings_Header.Header",
        "PingSettings_Desc.Text",
        "ScanRange_Header.Header",
        "ScanStartId_Header.Header",
        "ScanEndId_Header.Header",
        "PeriodicPing_Header.Header",
        "PeriodicPing_Desc.Text",
        "PingInterval_Header.Header",
        "PingInterval_Desc.Text",
        "PingCountryCode_Header.Header",
        "PingCountryCode_Desc.Text",
        "PingTimeZone_Header.Header",
        "PingPreview_Header.Header",
    ];

    [TestMethod]
    public void Resources_ContainSettingsReorganizationStrings()
    {
        foreach (string culture in new[] { "ko-KR", "en-US" })
        {
            XDocument resources = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "Strings", culture, "Resources.resw"));
            HashSet<string> names = resources.Root!
                .Elements("data")
                .Select(element => (string?)element.Attribute("name"))
                .Where(name => name is not null)
                .Cast<string>()
                .ToHashSet();

            foreach (string key in RequiredResourceKeys)
            {
                Assert.IsTrue(names.Contains(key), $"{culture} is missing {key}");
            }
        }
    }

    [TestMethod]
    public void Resources_RenamePingSettingsHeaderToSyncSettings()
    {
        XDocument ko = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "Strings", "ko-KR", "Resources.resw"));
        XDocument en = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "Strings", "en-US", "Resources.resw"));

        Assert.AreEqual("동기화 설정", GetResourceValue(ko, "PingSettings_Header.Header"));
        Assert.AreEqual("Sync Settings", GetResourceValue(en, "PingSettings_Header.Header"));
    }

    [TestMethod]
    public void SettingsPage_UsesThreeSectionsAndPingExpander()
    {
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SettingsPage.xaml"));
        string xaml = page.ToString(SaveOptions.DisableFormatting);

        StringAssert.Contains(xaml, "Section_Connection.Text");
        StringAssert.Contains(xaml, "Section_Communication.Text");
        StringAssert.Contains(xaml, "Section_App.Text");
        StringAssert.Contains(xaml, "XBee_Header.Header");
        StringAssert.Contains(xaml, "XBee_Desc.Text");
        StringAssert.Contains(xaml, "ResponseTimeout_Header.Header");
        StringAssert.Contains(xaml, "PingSettings_Header.Header");
        StringAssert.Contains(xaml, "PingPreview_Header.Header");

        Assert.AreEqual(3, CountOccurrences(xaml, "BodyStrongTextBlockStyle"));
        Assert.AreEqual(1, CountOccurrences(xaml, "IsVirtualModeEnabled"));
        Assert.IsTrue(xaml.IndexOf("VirtualMode_Header.Header", StringComparison.Ordinal) <
                      xaml.IndexOf("ConnectButtonText", StringComparison.Ordinal));
        Assert.IsTrue(xaml.IndexOf("PingSettings_Header.Header", StringComparison.Ordinal) <
                      xaml.IndexOf("PeriodicPing_Header.Header", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SettingsPage_ShowsSettingsFileOpenButtonsAtTop()
    {
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SettingsPage.xaml"));
        string xaml = page.ToString(SaveOptions.DisableFormatting);
        string codeBehind = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SettingsPage.xaml.cs"));

        StringAssert.Contains(xaml, "OpenAppSettingsFolderButton");
        StringAssert.Contains(xaml, "OpenSettingsFolder_Button");
        StringAssert.Contains(xaml, "OnOpenAppSettingsFolderClicked");
        StringAssert.Contains(xaml, "OpenAppSettingsFileButton");
        StringAssert.Contains(xaml, "OpenSettingsFile_Button");
        StringAssert.Contains(xaml, "OnOpenAppSettingsFileClicked");
        Assert.IsTrue(xaml.IndexOf("Settings_Title.Text", StringComparison.Ordinal) <
                      xaml.IndexOf("Section_Connection.Text", StringComparison.Ordinal));

        StringAssert.Contains(codeBehind, "AppSettingsFilePath");
        StringAssert.Contains(codeBehind, "notepad.exe");
        StringAssert.Contains(codeBehind, "explorer.exe");
    }

    [TestMethod]
    public void SettingsPage_ShowsThemeSelectorInAppSection()
    {
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SettingsPage.xaml"));
        string xaml = page.ToString(SaveOptions.DisableFormatting);
        string viewModel = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "ViewModels", "SettingsViewModel.cs"));

        StringAssert.Contains(xaml, "Theme_Header.Header");
        StringAssert.Contains(xaml, "Theme_Desc.Text");
        StringAssert.Contains(xaml, "ThemeOptions");
        StringAssert.Contains(xaml, "SelectedThemeOption");
        Assert.IsTrue(xaml.IndexOf("Section_App.Text", StringComparison.Ordinal) <
                      xaml.IndexOf("Theme_Header.Header", StringComparison.Ordinal));

        StringAssert.Contains(viewModel, "ThemeOptions");
        StringAssert.Contains(viewModel, "SelectedThemeOption");
    }

    [TestMethod]
    public void SettingsPage_ConsolidatesDuplicateComAndXBeePortSettings()
    {
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SettingsPage.xaml"));
        string xaml = page.ToString(SaveOptions.DisableFormatting);

        StringAssert.Contains(xaml, "XBee_Header.Header");
        StringAssert.Contains(xaml, "XBeePort_Header.Header");
        StringAssert.Contains(xaml, "LastPortAutoConnect_Header.Header");
        StringAssert.Contains(xaml, "VirtualMode_Header.Header");

        Assert.AreEqual(1, CountOccurrences(xaml, "AvailablePortOptions"));
        Assert.AreEqual(1, CountOccurrences(xaml, "AvailableBaudRates"));
        Assert.AreEqual(1, CountOccurrences(xaml, "IsLastPortAutoConnectEnabled"));
        Assert.AreEqual(1, CountOccurrences(xaml, "ConnectCommand"));
        Assert.AreEqual(0, CountOccurrences(xaml, "SelectedXBeePort"));
        Assert.AreEqual(0, CountOccurrences(xaml, "XBeeConnectCommand"));
        Assert.AreEqual(0, CountOccurrences(xaml, "Connection_Header.Header"));
        Assert.IsTrue(xaml.IndexOf("XBeePort_Header.Header", StringComparison.Ordinal) <
                      xaml.IndexOf("VirtualMode_Header.Header", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SettingsPage_ShowsUsbDeviceHintsWhileKeepingSelectedComPortValue()
    {
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SettingsPage.xaml"));
        string xaml = page.ToString(SaveOptions.DisableFormatting);

        StringAssert.Contains(xaml, "AvailablePortOptions");
        StringAssert.Contains(xaml, "DisplayMemberPath=\"DisplayName\"");
        StringAssert.Contains(xaml, "SelectedValuePath=\"PortName\"");
        StringAssert.Contains(xaml, "SelectedValue=\"{x:Bind ViewModel.SelectedPort, Mode=TwoWay}\"");
        Assert.AreEqual(0, CountOccurrences(xaml, "SelectedItem=\"{x:Bind ViewModel.SelectedPort"));
    }

    [TestMethod]
    public void SettingsPage_ResponseTimeoutAndPingIntervalUseTenthSecondSteps()
    {
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SettingsPage.xaml"));
        string xaml = page.ToString(SaveOptions.DisableFormatting);
        string codeBehind = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SettingsPage.xaml.cs"));
        string viewModel = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "ViewModels", "SettingsViewModel.cs"));

        StringAssert.Contains(xaml, "Value=\"{x:Bind ViewModel.ResponseTimeoutSeconds, Mode=TwoWay}\" Minimum=\"0.1\" Maximum=\"60\" SpinButtonPlacementMode=\"Inline\" SmallChange=\"0.1\"");
        StringAssert.Contains(xaml, "Value=\"{x:Bind ViewModel.PingIntervalSeconds, Mode=TwoWay}\" Minimum=\"0.1\" Maximum=\"60\" SpinButtonPlacementMode=\"Inline\" SmallChange=\"0.1\"");
        StringAssert.Contains(xaml, "x:Name=\"ResponseTimeoutNumberBox\"");
        StringAssert.Contains(xaml, "x:Name=\"PingIntervalNumberBox\"");
        StringAssert.Contains(codeBehind, "CreateOneDecimalFormatter");
        StringAssert.Contains(codeBehind, "FractionDigits = 1");
        StringAssert.Contains(codeBehind, "Increment = 0.1");
        StringAssert.Contains(viewModel, "Math.Round(value, 1)");
    }

    [TestMethod]
    public void SettingsPage_ShowsScanRangeInputsInSyncSettings()
    {
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SettingsPage.xaml"));
        string xaml = page.ToString(SaveOptions.DisableFormatting);

        StringAssert.Contains(xaml, "ScanRange_Header.Header");
        StringAssert.Contains(xaml, "ScanStartId_Header.Header");
        StringAssert.Contains(xaml, "Value=\"{x:Bind ViewModel.ScanStartId, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"254\"");
        StringAssert.Contains(xaml, "ScanEndId_Header.Header");
        StringAssert.Contains(xaml, "Value=\"{x:Bind ViewModel.ScanEndId, Mode=TwoWay}\" Minimum=\"1\" Maximum=\"254\"");
        Assert.IsTrue(xaml.IndexOf("PingSettings_Header.Header", StringComparison.Ordinal) <
                      xaml.IndexOf("ScanRange_Header.Header", StringComparison.Ordinal));
    }

    [TestMethod]
    public void SettingsViewModel_LoadsComPortsWithoutUsbMetadataScan()
    {
        string source = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "ViewModels", "SettingsViewModel.cs"));
        string dashboardSource = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "ViewModels", "DashboardViewModel.cs"));

        Assert.IsFalse(source.Contains("RefreshPortsAsync", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("SerialPortDeviceInfoProvider.GetDeviceInfoByPort", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Task.Run", StringComparison.Ordinal));
        StringAssert.Contains(source, "RefreshPorts();");
        StringAssert.Contains(source, "StartLastPortAutoConnectIfEnabled();");
        StringAssert.Contains(source, "IsLastPortAutoConnectEnabled");
        StringAssert.Contains(source, "_lastLoadedComPortForAutoConnect");
        StringAssert.Contains(source, "ConnectCoreAsync(autoScanAfterConnect: true)");
        StringAssert.Contains(source, "_dashboardViewModel.ScanConfiguredRangeAsync");
        StringAssert.Contains(dashboardSource, "ScanConfiguredRangeAsync");
        StringAssert.Contains(dashboardSource, "_serialService.ScanDevicesAsync(startId, endId)");
        StringAssert.Contains(source, "SerialPortDisplay.CreateOption(portName, deviceInfo: null)");
    }

    [TestMethod]
    public void ConnectionControls_UseVectorPlugIconsInsteadOfUnsupportedFontGlyphs()
    {
        string mainWindow = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml"));
        string settingsPage = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SettingsPage.xaml"));
        string converterSource = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Converters", "ConnectionConverters.cs"));

        Assert.IsFalse(mainWindow.Contains("ConnectionIconGlyph", StringComparison.Ordinal));
        Assert.IsFalse(settingsPage.Contains("ConnectionIconConverter", StringComparison.Ordinal));
        StringAssert.Contains(mainWindow, "ConnectedPlugIcon");
        StringAssert.Contains(mainWindow, "DisconnectedPlugIcon");
        StringAssert.Contains(settingsPage, "ConnectedPlugIcon");
        StringAssert.Contains(settingsPage, "DisconnectedPlugIcon");
        StringAssert.Contains(converterSource, "InverseBoolToVisibilityConverter");
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

    private static string GetResourceValue(XDocument resources, string name)
        => resources.Root!
            .Elements("data")
            .Single(element => (string?)element.Attribute("name") == name)
            .Element("value")!
            .Value;
}
