using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Protocol;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class BackendDashboardSyncServiceTests
{
    [TestMethod]
    public async Task Start_SendsDevicesAndDoesNotPing()
    {
        var settings = TestSettings();
        var serial = new FakeSerialService();
        var backend = new FakeBackendMonitoringService(expectedCalls: 2);
        var service = new BackendDashboardSyncService(backend, settings);

        service.ReplaceDevices(new[] { new Device(2), new Device(3) });
        service.Start();

        await backend.WaitAsync();
        service.Stop();

        Assert.AreEqual(0, serial.PingedDeviceIds.Count);
        Assert.AreEqual(2, backend.SentDevices.Count);
    }

    [TestMethod]
    public async Task Start_SendsSnapshotDevicesWithoutPinging()
    {
        var settings = TestSettings();
        var serial = new FakeSerialService();
        var backend = new FakeBackendMonitoringService(expectedCalls: 1);
        var service = new BackendDashboardSyncService(backend, settings);
        var device = new Device(2) { PowerStatus = "ON", MotionState = MotionState.Playing };

        service.ReplaceDevices(new[] { device });
        service.Start();

        await backend.WaitAsync();
        service.Stop();

        Assert.AreEqual(0, serial.PingedDeviceIds.Count);
        Assert.AreSame(device, backend.SentDevices[0]);
    }

    [TestMethod]
    public async Task Start_Disabled_DoesNotPingOrSend()
    {
        var settings = TestSettings();
        settings.IsBackendSyncEnabled = false;
        var serial = new FakeSerialService();
        var backend = new FakeBackendMonitoringService(expectedCalls: 1);
        var service = new BackendDashboardSyncService(backend, settings);

        service.ReplaceDevices(new[] { new Device(2) });
        service.Start();
        await Task.Delay(100);
        service.Stop();

        Assert.AreEqual(0, serial.PingedDeviceIds.Count);
        Assert.AreEqual(0, backend.SentDevices.Count);
    }

    [TestMethod]
    public async Task Start_MissingApiKey_DoesNotSend()
    {
        var settings = TestSettings();
        settings.BackendApiKey = string.Empty;
        var backend = new FakeBackendMonitoringService(expectedCalls: 1);
        var service = new BackendDashboardSyncService(backend, settings);

        service.ReplaceDevices(new[] { new Device(2) });
        service.Start();
        await Task.Delay(100);
        service.Stop();

        Assert.AreEqual(0, backend.SentDevices.Count);
    }

    [TestMethod]
    public async Task Start_SendsExistingDisconnectedSnapshot()
    {
        var settings = TestSettings();
        var backend = new FakeBackendMonitoringService(expectedCalls: 1);
        var service = new BackendDashboardSyncService(backend, settings);
        var device = new Device(2)
        {
            PowerStatus = "OFF",
            MotionState = MotionState.Stopped,
            StatusMessage = "Disconnected"
        };
        device.Motors.Add(new MotorState
        {
            Id = 2,
            Type = "DEVICE",
            Status = "Disconnected"
        });

        service.ReplaceDevices(new[] { device });
        service.Start();

        await backend.WaitAsync();
        service.Stop();

        Device sent = backend.SentDevices[0];
        Assert.AreEqual("OFF", sent.PowerStatus);
        Assert.AreEqual("Disconnected", sent.Motors[0].Status);
        Assert.AreEqual("DEVICE", sent.Motors[0].Type);
    }

    private static SettingsService TestSettings()
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ui_winui3_backend_dashboard_sync_tests", Guid.NewGuid().ToString("N"), "backend-settings.json");
        return new SettingsService(new FakeBackendSettingsPathProvider(path))
        {
            BackendSyncIntervalSeconds = 1,
            BackendApiKey = "api-key-1"
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

    private sealed class FakeBackendMonitoringService : IBackendMonitoringService
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int _expectedCalls;

        public FakeBackendMonitoringService(int expectedCalls)
        {
            _expectedCalls = expectedCalls;
        }

        public List<Device> SentDevices { get; } = new();

        public Task<BackendSendResult> SendObjectLogAsync(Device device, CancellationToken cancellationToken)
        {
            SentDevices.Add(device);
            if (SentDevices.Count >= _expectedCalls)
            {
                _completion.TrySetResult();
            }

            return Task.FromResult(new BackendSendResult(true, 200, "OK"));
        }

        public async Task WaitAsync()
        {
            Task completed = await Task.WhenAny(_completion.Task, Task.Delay(TimeSpan.FromSeconds(2)));
            Assert.AreSame(_completion.Task, completed);
        }
    }

    private sealed class FakeSerialService : ISerialService
    {
        public bool IsConnected => true;
        public Dictionary<int, Device?> PingResults { get; } = new();
        public List<int> PingedDeviceIds { get; } = new();

        public Task ConnectAsync(string portName, int baudRate) => Task.CompletedTask;
        public void Disconnect() { }
        public Task SendBinaryCommandAsync(int deviceId, byte[] packet) => Task.CompletedTask;
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, BinaryCommand cmd) => Task.FromResult<byte[]?>(null);
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, BinaryCommand cmd, byte[] packet) => Task.FromResult<byte[]?>(null);
        public Task<IEnumerable<Device>> ScanDevicesAsync(int startId, int endId) => Task.FromResult<IEnumerable<Device>>(Array.Empty<Device>());

        public Task<Device?> PingDeviceAsync(int deviceId)
        {
            PingedDeviceIds.Add(deviceId);
            return Task.FromResult(PingResults.TryGetValue(deviceId, out Device? device) ? device : null);
        }
    }
}
