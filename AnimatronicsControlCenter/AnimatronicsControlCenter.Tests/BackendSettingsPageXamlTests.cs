using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendSettingsPageXamlTests
{
    [TestMethod]
    public void BackendSettingsPage_ContainsServerAndLocalSettingsControls()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "BackendSettingsPage.xaml"));

        foreach (string expected in new[]
        {
            "ServerValuesPanel",
            "LocalSettingsPanel",
            "AvailableCountryCodes",
            "SelectedCountryCode",
            "ServerStoreList",
            "SelectedServerStore",
            "ServerPcList",
            "SelectedServerPc",
            "ServerObjectListHeader",
            "IsRegistrationAvailable",
            "ShouldShowRegistrationCountryCodeHint",
            "ApplyServerValuesCommand",
            "CompareWithServerCommand",
            "SaveCommand",
            "BackendBaseUrl",
            "IsServerConnectionEditing",
            "ServerConnectionEditButtonText",
            "ToggleServerConnectionEditingCommand",
            "BackendStoreId",
            "BackendPcId",
            "BackendPcName",
            "BackendSoftwareVersion",
            "BackendDeviceObjectMappingsText",
            "MappedServerObjects",
            "EditObjectMappingsButton",
            "OnEditObjectMappingsClicked",
            "OpenLocalSettingsFileButton",
            "OnOpenLocalSettingsFileClicked",
        })
        {
            StringAssert.Contains(xaml, expected);
        }
    }

    [TestMethod]
    public void BackendSettingsPage_UsesNativeSettingsLayout()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "BackendSettingsPage.xaml"));

        foreach (string expected in new[]
        {
            "BackendSettingsCommandBar",
            "ApplyServerValuesButton",
            "CompareWithServerButton",
            "SaveSettingsButton",
            "ServerConnectionInfoBar",
            "ConnectionSettingsExpander",
            "MappingJsonExpander",
            "SettingsSectionHeaderStyle",
        })
        {
            StringAssert.Contains(xaml, expected);
        }

        Assert.IsFalse(xaml.Contains("<Button Grid.Column=\"1\"", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BackendSettingsPage_ShowsCommandResultMessageBesideCommandButtons()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "BackendSettingsPage.xaml"));

        StringAssert.Contains(xaml, "CommandResultMessageContainer");
        StringAssert.Contains(xaml, "<AppBarElementContainer x:Name=\"CommandResultMessageContainer\"");
        StringAssert.Contains(xaml, "CommandResultMessage");
        StringAssert.Contains(xaml, "CommandResultMessageIcon");
        StringAssert.Contains(xaml, "LocalStatusMessage");
        StringAssert.Contains(xaml, "CornerRadius=\"6\"");
        StringAssert.Contains(xaml, "BorderBrush=\"{ThemeResource SystemControlHighlightAccentBrush}\"");
        StringAssert.Contains(xaml, "Background=\"{ThemeResource SystemControlBackgroundAccentBrush}\"");
        StringAssert.Contains(xaml, "HorizontalAlignment=\"Right\"");
        Assert.IsTrue(
            xaml.IndexOf("CommandResultMessageContainer", StringComparison.Ordinal) <
            xaml.IndexOf("ApplyServerValuesButton", StringComparison.Ordinal),
            "Command result message should appear before the apply/compare/save command buttons.");
        Assert.IsTrue(
            xaml.IndexOf("</CommandBar.Content>", StringComparison.Ordinal) <
            xaml.IndexOf("CommandResultMessageContainer", StringComparison.Ordinal),
            "Command result message should be in the command elements area, not the title content area.");
        Assert.IsFalse(xaml.Contains("LocalStatusInfoBar", StringComparison.Ordinal));
    }

    [TestMethod]
    public void BackendSettingsPage_PlacesApplyServerValuesButtonInsideServerSelectionFlow()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "BackendSettingsPage.xaml"));

        int serverValuesPanelIndex = xaml.IndexOf("ServerValuesPanel", StringComparison.Ordinal);
        int applyButtonIndex = xaml.IndexOf("ApplyServerValuesButton", StringComparison.Ordinal);
        int connectionSettingsIndex = xaml.IndexOf("ConnectionSettingsExpander", StringComparison.Ordinal);
        int commandBarIndex = xaml.IndexOf("BackendSettingsCommandBar", StringComparison.Ordinal);

        Assert.IsTrue(serverValuesPanelIndex >= 0, "Server selection panel should exist.");
        Assert.IsTrue(applyButtonIndex >= 0, "Apply button should exist.");
        Assert.IsTrue(connectionSettingsIndex >= 0, "Connection settings expander should exist.");
        Assert.IsTrue(commandBarIndex >= 0, "Command bar should exist.");
        Assert.IsTrue(
            serverValuesPanelIndex < applyButtonIndex && applyButtonIndex < connectionSettingsIndex,
            "Apply server values should live inside the server selection flow before connection settings.");
        Assert.IsFalse(
            commandBarIndex < applyButtonIndex && applyButtonIndex < serverValuesPanelIndex,
            "Apply server values should not stay in the top command bar.");
        Assert.IsTrue(
            xaml.IndexOf("Text=\"서버 값 적용\"", StringComparison.Ordinal) <
            xaml.IndexOf("Symbol=\"Forward\"", StringComparison.Ordinal),
            "Apply server values should show the label before the right arrow icon.");
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
