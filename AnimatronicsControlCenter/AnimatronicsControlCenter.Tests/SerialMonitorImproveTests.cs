using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class SerialMonitorImproveTests
{
    // ── Task 1 ──────────────────────────────────────────────────────────────
    [TestMethod]
    public void SerialTrafficEntry_HasDirectionArrow()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "Core", "Models", "SerialTrafficEntry.cs"));

        StringAssert.Contains(code, "DirectionArrow");
        StringAssert.Contains(code, "\"↑\"");
        StringAssert.Contains(code, "\"↓\"");
    }

    [TestMethod]
    public void SerialDirectionBrushConverter_UsesThemeAwareTxBlueRxGreen()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "Converters", "SerialDirectionToBrushConverter.cs"));

        StringAssert.Contains(code, "DarkTxBrush");
        StringAssert.Contains(code, "DarkRxBrush");
        StringAssert.Contains(code, "LightTxBrush");
        StringAssert.Contains(code, "LightRxBrush");
        StringAssert.Contains(code, "AppThemeHelper.IsLightTheme()");
    }

    // ── Task 2 ──────────────────────────────────────────────────────────────
    [TestMethod]
    public void SerialPage_DataTemplate_ShowsDirectionArrowAndTimestamp()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "Views", "SerialMonitorPage.xaml"));

        StringAssert.Contains(xaml, "DirectionArrow");
        StringAssert.Contains(xaml, "TimestampText");
        // 바이트 열은 Line 속성 직접 바인딩
        StringAssert.Contains(xaml, "x:Bind Line");
    }

    // ── Task 3 ──────────────────────────────────────────────────────────────
    [TestMethod]
    public void SerialPage_LineTextUsesThemeForegroundInsteadOfDirectionBrush()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "Views", "SerialMonitorPage.xaml"));
        string normalizedXaml = xaml.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.AreEqual(2, CountOccurrences(normalizedXaml, "Text=\"{x:Bind Line}\""));
        Assert.AreEqual(2, CountOccurrences(normalizedXaml, "Text=\"{x:Bind Line}\"\n                                                   FontFamily=\"Consolas\"\n                                                   FontSize=\"12\"\n                                                   Foreground=\"{ThemeResource SystemControlForegroundBaseHighBrush}\""));
    }

    [TestMethod]
    public void SerialMonitorViewModel_HasPacketDirectionFilter()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "ViewModels", "SerialMonitorViewModel.cs"));

        StringAssert.Contains(code, "PacketDirectionFilters");
        StringAssert.Contains(code, "selectedPacketDirectionFilter");
        StringAssert.Contains(code, "PacketSrcIdFilters");
        StringAssert.Contains(code, "selectedPacketSrcIdFilter");
        StringAssert.Contains(code, "PacketTarIdFilters");
        StringAssert.Contains(code, "selectedPacketTarIdFilter");
    }

    [TestMethod]
    public void SerialMonitorViewModel_MatchesPacketFilter_UsesDirectionFilter()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "ViewModels", "SerialMonitorViewModel.cs"));

        StringAssert.Contains(code, "SelectedPacketDirectionFilter");
        StringAssert.Contains(code, "↑ 송신");
        StringAssert.Contains(code, "↓ 수신");
        StringAssert.Contains(code, "SelectedPacketSrcIdFilter");
        StringAssert.Contains(code, "SelectedPacketTarIdFilter");
    }

    // ── Task 4 ──────────────────────────────────────────────────────────────
    [TestMethod]
    public void SerialPage_PacketTab_HasDirectionSrcTarFilterControls()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "Views", "SerialMonitorPage.xaml"));

        StringAssert.Contains(xaml, "PacketDirectionFilters");
        StringAssert.Contains(xaml, "SelectedPacketDirectionFilter");
        StringAssert.Contains(xaml, "PacketSrcIdFilters");
        StringAssert.Contains(xaml, "SelectedPacketSrcIdFilter");
        StringAssert.Contains(xaml, "PacketTarIdFilters");
        StringAssert.Contains(xaml, "SelectedPacketTarIdFilter");
    }

    [TestMethod]
    public void SerialPage_PacketTab_FilterControlsUseCenteredAlignment()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "Views", "SerialMonitorPage.xaml"));

        StringAssert.Contains(xaml, "x:Name=\"PacketFilterBar\"");
        StringAssert.Contains(xaml, "VerticalAlignment=\"Center\"");
        StringAssert.Contains(xaml, "ComboBox VerticalAlignment=\"Center\"");
        Assert.IsFalse(xaml.Contains("ToggleSwitch Header=\"오류만\"", StringComparison.Ordinal));
        StringAssert.Contains(xaml, "Text=\"오류만\"");
    }

    // ── Task 5 ──────────────────────────────────────────────────────────────
    [TestMethod]
    public void SerialPage_HasStatusBar()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter",
            "UI", "Views", "SerialMonitorPage.xaml"));

        StringAssert.Contains(xaml, "statusbar");
        StringAssert.Contains(xaml, "ViewModel.TxCount");
        StringAssert.Contains(xaml, "ViewModel.RxCount");
        StringAssert.Contains(xaml, "ViewModel.IsPaused");
    }

    private static string ProjectPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(candidate))
                return candidate;
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
}
