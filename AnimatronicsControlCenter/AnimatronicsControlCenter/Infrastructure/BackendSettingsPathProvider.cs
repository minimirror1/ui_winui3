using System;
using System.IO;
using AnimatronicsControlCenter.Core.Interfaces;
#if WINDOWS
using Windows.Storage;
#endif

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendSettingsPathProvider : IBackendSettingsPathProvider
{
    public string BackendSettingsFilePath { get; } = Path.Combine(GetSettingsDirectory(), "backend-settings.json");

    private static string GetSettingsDirectory()
    {
#if WINDOWS
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch
        {
        }
#endif

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AnimatronicsControlCenter");
    }
}
