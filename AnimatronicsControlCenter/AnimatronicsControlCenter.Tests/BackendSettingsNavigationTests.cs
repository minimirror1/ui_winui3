using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendSettingsNavigationTests
{
    [TestMethod]
    public void MainWindow_HasBackendSettingsFooterButtonAndNavigationHandler()
    {
        string xaml = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml"));
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml.cs"));

        StringAssert.Contains(xaml, "BackendSettingsButton");
        StringAssert.Contains(xaml, "BackendSettingsButton_Click");
        StringAssert.Contains(xaml, "Backend settings");
        StringAssert.Contains(code, "BackendSettingsButton_Click");
        StringAssert.Contains(code, "ContentFrame.Navigate(typeof(BackendSettingsPage))");
    }

    [TestMethod]
    public void BackendSettingsPage_LoadsViewModelFromServices()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "UI", "Views", "BackendSettingsPage.xaml.cs"));

        StringAssert.Contains(code, "BackendSettingsViewModel ViewModel");
        StringAssert.Contains(code, "GetRequiredService<BackendSettingsViewModel>()");
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
