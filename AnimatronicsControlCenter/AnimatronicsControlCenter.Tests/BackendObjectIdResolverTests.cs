using System;
using System.Collections.Generic;
using System.IO;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendObjectIdResolverTests
{
    [TestMethod]
    public void ResolveObjectId_ReturnsMappedObjectId()
    {
        var settings = new SettingsService(new FakeBackendSettingsPathProvider(CreateTempSettingsPath()))
        {
            BackendDeviceObjectMappings = new Dictionary<int, string>
            {
                [2] = "obj-1"
            }
        };
        var resolver = new BackendObjectIdResolver(settings);

        Assert.AreEqual("obj-1", resolver.ResolveObjectId(2));
    }

    [TestMethod]
    public void ResolveObjectId_ReturnsNullForMissingOrEmptyMapping()
    {
        var settings = new SettingsService(new FakeBackendSettingsPathProvider(CreateTempSettingsPath()))
        {
            BackendDeviceObjectMappings = new Dictionary<int, string>
            {
                [3] = "   "
            }
        };
        var resolver = new BackendObjectIdResolver(settings);

        Assert.IsNull(resolver.ResolveObjectId(2));
        Assert.IsNull(resolver.ResolveObjectId(3));
    }

    private static string CreateTempSettingsPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ui_winui3_backend_resolver_tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "backend-settings.json");
    }

    private sealed class FakeBackendSettingsPathProvider : IBackendSettingsPathProvider
    {
        public FakeBackendSettingsPathProvider(string filePath)
        {
            BackendSettingsFilePath = filePath;
        }

        public string BackendSettingsFilePath { get; }
    }
}
