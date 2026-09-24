using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class TrafficLogRegistrationTests
{
    [TestMethod]
    public void App_RegistersTrafficFileLoggerBesideTheSettingsFile()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "App.xaml.cs"));

        StringAssert.Contains(code, "AddSingleton<TrafficFileLogger>");
        StringAssert.Contains(code, "LogFilePolicy.FolderFor");
        StringAssert.Contains(code, "LogFilePolicy.DefaultMaxFileBytes");
    }

    [TestMethod]
    public void App_SubscribesTheLoggerToTheTrafficTapOnLaunch()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "App.xaml.cs"));

        StringAssert.Contains(code, "logger.Subscribe(Services.GetRequiredService<ISerialTrafficTap>());");
    }

    [TestMethod]
    public void App_ClearsOldRecordsBeforeItStartsLogging()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "App.xaml.cs"));

        StringAssert.Contains(code, "LogFileCleaner.Clean");
        StringAssert.Contains(code, "LogFilePolicy.DefaultRetentionDays");
        StringAssert.Contains(code, "LogFilePolicy.DefaultMaxTotalBytes");
    }

    [TestMethod]
    public void App_AppliesTheSavedLogSettingsOnLaunch()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "App.xaml.cs"));

        StringAssert.Contains(code, "logger.IsEnabled = settingsService.IsTrafficLogEnabled;");
        StringAssert.Contains(code, "logger.IncludeRaw = settingsService.IsTrafficLogRawIncluded;");
    }

    [TestMethod]
    public void MainWindow_TreatsTheLogViewerLikeTheOtherSecondaryPages()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "MainWindow.xaml.cs"));

        StringAssert.Contains(code, "typeof(LogViewerPage)");
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

        throw new FileNotFoundException($"찾지 못함: {string.Join('/', segments)}");
    }
}
