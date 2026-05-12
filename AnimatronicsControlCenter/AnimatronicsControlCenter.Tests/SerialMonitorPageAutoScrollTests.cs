using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class SerialMonitorPageAutoScrollTests
{
    [TestMethod]
    public void ComRawAutoScroll_IsDispatchedAfterLayout()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SerialMonitorPage.xaml.cs"));

        StringAssert.Contains(code, "ScrollToBottomAfterLayout(ComRawScroll)");
        StringAssert.Contains(code, "DispatcherQueue.TryEnqueue");
        StringAssert.Contains(code, "scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null)");
    }

    [TestMethod]
    public void PacketAutoScroll_ScrollsLeftPacketListOnNewPackets()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SerialMonitorPage.xaml"));
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "SerialMonitorPage.xaml.cs"));

        StringAssert.Contains(xaml, "x:Name=\"PacketList\"");
        StringAssert.Contains(code, "ViewModel.Packets.CollectionChanged += Packets_CollectionChanged");
        StringAssert.Contains(code, "ViewModel.SelectedTabIndex != 1");
        StringAssert.Contains(code, "ScrollListToLastItemAfterLayout(PacketList, ViewModel.Packets)");
        StringAssert.Contains(code, "listView.ScrollIntoView(items[^1])");
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
