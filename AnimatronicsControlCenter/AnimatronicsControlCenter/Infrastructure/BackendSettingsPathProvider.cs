using System;
using System.IO;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class BackendSettingsPathProvider : IBackendSettingsPathProvider
{
    public string BackendSettingsFilePath { get; } = Path.Combine(AppContext.BaseDirectory, "backend-settings.json");
}
