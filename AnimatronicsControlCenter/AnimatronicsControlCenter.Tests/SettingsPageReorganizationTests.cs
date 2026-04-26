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
        "XBee_Header.Header",
        "XBee_Desc.Text",
        "XBeePort_Header.Header",
        "ResponseTimeout_Header.Header",
        "ResponseTimeout_Desc.Text",
        "PingSettings_Header.Header",
        "PingSettings_Desc.Text",
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
}
