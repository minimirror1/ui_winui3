using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Backend;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendServerCatalogClientTests
{
    [TestMethod]
    public async Task GetStoreDetailAsync_UsesAbsoluteStoreDetailEndpointAndReadsResponse()
    {
        using var handler = new RecordingHandler("""{"store_id":"store-1","store_name":"Main","country_code":"KR","pcs":[{"pc_id":"pc-1","pc_name":"PC","sw_version":"1.1.1.0","objects":[]}]}""");
        using var httpClient = new HttpClient(handler);
        var settings = TestSettings("https://example.invalid/api");
        settings.BackendBearerToken = "token-1";
        settings.IsBackendSyncEnabled = false;
        var client = new BackendServerCatalogClient(httpClient, settings);

        var result = await client.GetStoreDetailAsync("store-1", CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual("store-1", result.Data!.StoreId);
        Assert.AreEqual(HttpMethod.Get, handler.Request!.Method);
        Assert.AreEqual("https://example.invalid/api/v1/service/stores/store-1/detail", handler.Request.RequestUri!.ToString());
        Assert.AreEqual("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.AreEqual("token-1", handler.Request.Headers.Authorization.Parameter);
    }

    [TestMethod]
    public async Task UpdatePcMetadataAsync_PutsPcNameAndSoftwareVersion()
    {
        using var handler = new RecordingHandler("""{"ok":true}""");
        using var httpClient = new HttpClient(handler);
        var client = new BackendServerCatalogClient(httpClient, TestSettings("https://example.invalid"));

        var result = await client.UpdatePcMetadataAsync(
            "store-1",
            "pc-1",
            new BackendPcUpdateRequest("pc_name_001", "1.1.1.0"),
            CancellationToken.None);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(HttpMethod.Put, handler.Request!.Method);
        Assert.AreEqual("https://example.invalid/v1/service/stores/store-1/pcs/pc-1", handler.Request.RequestUri!.ToString());
        StringAssert.Contains(handler.Body!, "\"pc_name\":\"pc_name_001\"");
        StringAssert.Contains(handler.Body!, "\"sw_version\":\"1.1.1.0\"");
    }

    [TestMethod]
    public async Task GetStoreDetailAsync_EmptyToken_DoesNotSendAuthorizationHeader()
    {
        using var handler = new RecordingHandler("""{"store_id":"store-1","store_name":null,"country_code":null,"pcs":[]}""");
        using var httpClient = new HttpClient(handler);
        var settings = TestSettings("https://example.invalid");
        settings.BackendBearerToken = "";
        var client = new BackendServerCatalogClient(httpClient, settings);

        await client.GetStoreDetailAsync("store-1", CancellationToken.None);

        Assert.IsNull(handler.Request!.Headers.Authorization);
    }

    [TestMethod]
    public async Task GetStoreDetailAsync_NonSuccess_ReturnsFailureResult()
    {
        using var handler = new RecordingHandler("nope", HttpStatusCode.BadRequest);
        using var httpClient = new HttpClient(handler);
        var client = new BackendServerCatalogClient(httpClient, TestSettings("https://example.invalid"));

        var result = await client.GetStoreDetailAsync("store-1", CancellationToken.None);

        Assert.IsFalse(result.Success);
        Assert.AreEqual(400, result.StatusCode);
        StringAssert.Contains(result.Message, "nope");
    }

    private static SettingsService TestSettings(string baseUrl)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ui_winui3_backend_http_tests", Guid.NewGuid().ToString("N"), "backend-settings.json");
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
