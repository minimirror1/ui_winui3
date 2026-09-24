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

        StringAssert.Contains(code, "GetRequiredService<TrafficFileLogger>().Subscribe(");
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
