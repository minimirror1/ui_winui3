using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendMonitoringServiceTests
{
    [TestMethod]
    public async Task SendObjectLogAsync_PostsLogToMappedObject()
    {
        using var handler = new RecordingHandler("""{"ok":true}""");
        using var httpClient = new HttpClient(handler);
        var settings = TestSettings("https://example.invalid");
        settings.BackendDeviceObjectMappings = new Dictionary<int, string> { [2] = "obj-1" };
        var resolver = new BackendObjectIdResolver(settings);
        var service = new BackendMonitoringService(httpClient, settings, resolver);
        var device = new Device(2) { MotionState = MotionState.Playing };

        var result = await service.SendObjectLogAsync(device, CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(HttpMethod.Post, handler.Request!.Method);
        Assert.AreEqual("https://example.invalid/v1/service/objects/obj-1/logs", handler.Request.RequestUri!.ToString());
        StringAssert.Contains(handler.Body!, "\"power_status\"");
        StringAssert.Contains(handler.Body!, "\"operation_status\":\"PLAY\"");
        StringAssert.Contains(handler.Body!, "\"error_data\"");
        Assert.IsFalse(handler.Request.RequestUri.ToString().Contains("/power", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task SendObjectLogAsync_MissingObjectMapping_DoesNotSendNetworkRequest()
    {
        using var handler = new RecordingHandler("""{"ok":true}""");
        using var httpClient = new HttpClient(handler);
        var settings = TestSettings("https://example.invalid");
        var resolver = new BackendObjectIdResolver(settings);
        var service = new BackendMonitoringService(httpClient, settings, resolver);

        var result = await service.SendObjectLogAsync(new Device(2), CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.IsNull(handler.Request);
    }

    [TestMethod]
    public async Task SendObjectLogAsync_Disabled_ReturnsFailureWithoutNetworkRequest()
    {
        using var handler = new RecordingHandler("""{"ok":true}""");
        using var httpClient = new HttpClient(handler);
        var settings = TestSettings("https://example.invalid");
        settings.IsBackendSyncEnabled = false;
        settings.BackendDeviceObjectMappings = new Dictionary<int, string> { [2] = "obj-1" };
        var resolver = new BackendObjectIdResolver(settings);
        var service = new BackendMonitoringService(httpClient, settings, resolver);

        var result = await service.SendObjectLogAsync(new Device(2), CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.AreEqual("Backend sync disabled.", result.Message);
        Assert.IsNull(handler.Request);
    }

    private static SettingsService TestSettings(string baseUrl)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ui_winui3_backend_monitoring_tests", Guid.NewGuid().ToString("N"), "backend-settings.json");
        return new SettingsService(new FakeBackendSettingsPathProvider(path))
        {
            BackendBaseUrl = baseUrl
        };
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

public sealed class RecordingHandler : HttpMessageHandler
{
    private readonly string _responseBody;
    private readonly HttpStatusCode _statusCode;

    public RecordingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseBody = responseBody;
        _statusCode = statusCode;
    }

    public HttpRequestMessage? Request { get; private set; }
    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody)
        };
    }
}
