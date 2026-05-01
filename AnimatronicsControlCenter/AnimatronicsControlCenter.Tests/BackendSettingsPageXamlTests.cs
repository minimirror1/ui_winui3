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
            "FetchServerCommand",
            "ApplyServerValuesCommand",
            "CompareWithServerCommand",
            "SaveCommand",
            "BackendBaseUrl",
            "BackendStoreId",
            "BackendPcId",
            "BackendPcName",
            "BackendSoftwareVersion",
            "BackendDeviceObjectMappingsText"
        })
        {
            StringAssert.Contains(xaml, expected);
        }
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
