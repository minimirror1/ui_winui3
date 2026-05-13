using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Protocol;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendPowerSseServiceTests
{
    [TestMethod]
    public async Task Start_OpensPowerSseForMappedObjectAndLogsPayload()
    {
        using var handler = new SseHandler("data: {\"power_status\":\"ON\"}\n\n");
        var settings = TestSettings("https://example.invalid");
        settings.BackendDeviceObjectMappings = new Dictionary<int, string> { [2] = "obj-1" };
        var trafficTap = new BackendTrafficTap();
        using var service = new BackendPowerSseService(settings, trafficTap, handler);

        service.Start();

        await WaitUntilAsync(() => trafficTap.GetEntries().Count >= 3);
        service.Stop();

        Assert.AreEqual(HttpMethod.Get, handler.Request!.Method);
        Assert.AreEqual("https://example.invalid/v1/service/objects/obj-1/power", handler.Request.RequestUri!.ToString());

        var entries = trafficTap.GetEntries();
        Assert.IsTrue(entries.Any(entry => entry.Message.Contains("{\"power_status\":\"ON\"}", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task Start_ForwardsPowerStatusOnToMappedDeviceAsPowerCtrl()
    {
        string body = """
            data: {"eventType":"COMMAND","data":"{\"power_status\":\"ON\",\"object_id\":\"obj-1\"}"}

            """;
        using var handler = new SseHandler(body);
        var settings = TestSettings("https://example.invalid");
        settings.BackendDeviceObjectMappings = new Dictionary<int, string> { [2] = "obj-1" };
        var trafficTap = new BackendTrafficTap();
        var serial = new FakeSerialService();
        using var service = new BackendPowerSseService(settings, trafficTap, serial, handler);

        service.Start();

        await WaitUntilAsync(() => serial.SentPackets.Count == 1);
        service.Stop();

        Assert.AreEqual(2, serial.SentDeviceIds[0]);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x02, 0x05, 0x01, 0x00, 0x01 }, serial.SentPackets[0]);
    }

    [TestMethod]
    public async Task Start_ForwardsPowerActionRebootToMappedDeviceAsPowerCtrl()
    {
        string body = """
            data: {"eventType":"COMMAND","data":"{\"power_action\":\"REBOOT\",\"object_id\":\"obj-1\"}"}

            """;
        using var handler = new SseHandler(body);
        var settings = TestSettings("https://example.invalid");
        settings.BackendDeviceObjectMappings = new Dictionary<int, string> { [2] = "obj-1" };
        var trafficTap = new BackendTrafficTap();
        var serial = new FakeSerialService();
        using var service = new BackendPowerSseService(settings, trafficTap, serial, handler);

        service.Start();

        await WaitUntilAsync(() => serial.SentPackets.Count == 1);
        service.Stop();

        CollectionAssert.AreEqual(new byte[] { 0x00, 0x02, 0x05, 0x01, 0x00, 0x02 }, serial.SentPackets[0]);
    }

    private static SettingsService TestSettings(string baseUrl)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ui_winui3_backend_power_sse_tests", Guid.NewGuid().ToString("N"), "backend-settings.json");
        return new SettingsService(new FakeBackendSettingsPathProvider(path))
        {
            BackendBaseUrl = baseUrl
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition())
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }
    }

    private sealed class FakeBackendSettingsPathProvider : IBackendSettingsPathProvider
    {
        public FakeBackendSettingsPathProvider(string filePath)
        {
            BackendSettingsFilePath = filePath;
        }

        public string BackendSettingsFilePath { get; }
    }

    private sealed class SseHandler : HttpMessageHandler
    {
        private readonly string _body;

        public SseHandler(string body)
        {
            _body = body;
        }

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body)
            });
        }
    }

    private sealed class FakeSerialService : ISerialService
    {
        public bool IsConnected => true;
        public List<int> SentDeviceIds { get; } = new();
        public List<byte[]> SentPackets { get; } = new();

        public Task ConnectAsync(string portName, int baudRate) => Task.CompletedTask;
        public void Disconnect() { }

        public Task SendBinaryCommandAsync(int deviceId, byte[] packet)
        {
            SentDeviceIds.Add(deviceId);
            SentPackets.Add(packet);
            return Task.CompletedTask;
        }

        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, BinaryCommand cmd) => Task.FromResult<byte[]?>(null);
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, BinaryCommand cmd, byte[] packet) => Task.FromResult<byte[]?>(null);
        public Task<Device?> PingDeviceAsync(int deviceId) => Task.FromResult<Device?>(null);
        public Task<IEnumerable<Device>> ScanDevicesAsync(int startId, int endId) => Task.FromResult<IEnumerable<Device>>(Array.Empty<Device>());
    }
}
