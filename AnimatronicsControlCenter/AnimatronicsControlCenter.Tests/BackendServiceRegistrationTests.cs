using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendServiceRegistrationTests
{
    [TestMethod]
    public void App_RegistersBackendServices()
    {
        string code = File.ReadAllText(ProjectPath("AnimatronicsControlCenter", "App.xaml.cs"));

        foreach (string expected in new[]
        {
            "AddSingleton<HttpClient>",
            "AddSingleton<IBackendObjectIdResolver, BackendObjectIdResolver>",
            "AddSingleton<IBackendSettingsPathProvider, BackendSettingsPathProvider>",
            "AddSingleton<IBackendMonitoringService, BackendMonitoringService>",
            "AddSingleton<IBackendServerCatalogClient, BackendServerCatalogClient>",
            "AddSingleton<IBackendDashboardSyncService, BackendDashboardSyncService>",
            "AddTransient<BackendSettingsViewModel>"
        })
        {
            StringAssert.Contains(code, expected);
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
