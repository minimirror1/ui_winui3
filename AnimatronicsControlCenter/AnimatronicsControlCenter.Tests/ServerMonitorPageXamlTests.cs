using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class ServerMonitorPageXamlTests
{
    [TestMethod]
    public void ServerMonitorPage_ShowsStatusSummaryAndTrafficList()
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument page = XDocument.Load(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "ServerMonitorPage.xaml"));

        string text = page.ToString();
        StringAssert.Contains(text, "Server URL");
        StringAssert.Contains(text, "Connection");
        StringAssert.Contains(text, "Last success");
        StringAssert.Contains(text, "Last failure");
        StringAssert.Contains(text, "Recent backend traffic");
        StringAssert.Contains(text, "Copy all");
        StringAssert.Contains(text, "Copy selected");

        XElement trafficList = page.Descendants(xaml + "ListView").Single(element => (string?)element.Attribute(x + "Name") == "TrafficList");
        Assert.AreEqual("Multiple", (string?)trafficList.Attribute("SelectionMode"));
        Assert.IsTrue(page.Descendants(xaml + "Button").Any(element => (string?)element.Attribute(x + "Name") == "CopyAllTrafficButton"));
        Assert.IsTrue(page.Descendants(xaml + "Button").Any(element => (string?)element.Attribute(x + "Name") == "CopySelectedTrafficButton"));
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
