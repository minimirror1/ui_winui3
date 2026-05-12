using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendSettingsPathProviderTests
{
    [TestMethod]
    public void BackendSettingsPathProvider_UsesApplicationLocalFolderForPackagedApps()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "Infrastructure", "BackendSettingsPathProvider.cs"));

        StringAssert.Contains(code, "ApplicationData.Current.LocalFolder.Path");
        Assert.IsFalse(code.Contains("Path.Combine(AppContext.BaseDirectory, \"backend-settings.json\")", StringComparison.Ordinal));
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
