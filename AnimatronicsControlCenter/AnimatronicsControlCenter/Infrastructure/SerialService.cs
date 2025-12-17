using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Infrastructure
{
    public class SerialService : ISerialService
    {
        private const byte HostId = 0;
        private const byte BroadcastId = 255;

        private SerialPort? _serialPort;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly ISettingsService _settingsService;
        private readonly ISerialTrafficTap _trafficTap;
        private readonly VirtualDeviceManager _virtualDeviceManager;
        private bool _isVirtualConnected;

        public SerialService(ISettingsService settingsService, ISerialTrafficTap trafficTap)
        {
            _settingsService = settingsService;
            _trafficTap = trafficTap;
            _virtualDeviceManager = new VirtualDeviceManager();
        }

        public bool IsConnected
        {
            get
            {
                if (_settingsService.IsVirtualModeEnabled)
                    return _isVirtualConnected;
                return _serialPort?.IsOpen ?? false;
            }
        }

        public async Task ConnectAsync(string portName, int baudRate)
        {
            if (_settingsService.IsVirtualModeEnabled)
            {
                _isVirtualConnected = true;
                await Task.CompletedTask;
                return;
            }

            if (_serialPort != null && _serialPort.IsOpen)
                Disconnect();

            _serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            try 
            {
                _serialPort.Open();
            }
            catch (Exception)
            {
                // Handle open failure if needed
                throw;
            }
            await Task.CompletedTask;
        }

        public void Disconnect()
        {
            _isVirtualConnected = false;

            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }
            _serialPort?.Dispose();
            _serialPort = null;
        }

        public async Task SendCommandAsync(int deviceId, string command, object? payload = null)
        {
            if (!IsConnected) return;

            // Firmware expects addressed packets: src_id/tar_id/cmd/payload (u8 IDs) + newline framing.
            byte tarId = checked((byte)deviceId);
            var message = new { src_id = HostId, tar_id = tarId, cmd = command, payload };
            var json = JsonSerializer.Serialize(message);
            _trafficTap.RecordTx(json + "\\n");
            
            if (_settingsService.IsVirtualModeEnabled)
            {
                // In virtual mode, we don't "send" and forget, we process.
                // But this method signature is void/Task.
                // So we just simulate the send.
                _virtualDeviceManager.ProcessCommand(json);
                await Task.CompletedTask;
                return;
            }

            await _writeLock.WaitAsync();
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.WriteLine(json);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task<string?> SendQueryAsync(int deviceId, string command, object? payload = null)
        {
            if (!IsConnected) return null;

            // Firmware expects addressed packets: src_id/tar_id/cmd/payload (u8 IDs) + newline framing.
            byte tarId = checked((byte)deviceId);
            var message = new { src_id = HostId, tar_id = tarId, cmd = command, payload };
            var json = JsonSerializer.Serialize(message);
            _trafficTap.RecordTx(json + "\\n");

            if (_settingsService.IsVirtualModeEnabled)
            {
                await Task.Delay(20);
                var response = _virtualDeviceManager.ProcessCommand(json);
                if (!string.IsNullOrEmpty(response))
                {
                    _trafficTap.RecordRx(response + "\\n");
                }
                return response;
            }

            await _writeLock.WaitAsync();
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.WriteLine(json);
                    
                    // Wait for response (simplified)
                    int retries = 5;
                    while (retries > 0)
                    {
                        await Task.Delay(100);
                        if (_serialPort.BytesToRead > 0)
                        {
                            var response = _serialPort.ReadLine();
                            if (!string.IsNullOrWhiteSpace(response))
                            {
                                _trafficTap.RecordRx(response + "\\n");
                            }
                            return response;
                        }
                        retries--;
                    }
                }
            }
            catch
            {
                // handle error
            }
            finally
            {
                _writeLock.Release();
            }
            return null;
        }

        public async Task<Device?> PingDeviceAsync(int deviceId)
        {
            if (!IsConnected) return null;

            if (_settingsService.IsVirtualModeEnabled)
            {
                await Task.Delay(20); // Simulate latency
                byte tarId = checked((byte)deviceId);
                var cmd = new { src_id = HostId, tar_id = tarId, cmd = "ping" };
                var jsonCmd = JsonSerializer.Serialize(cmd);
                _trafficTap.RecordTx(jsonCmd + "\\n");
                var responseJson = _virtualDeviceManager.ProcessCommand(jsonCmd);
                
                if (!string.IsNullOrEmpty(responseJson))
                {
                     _trafficTap.RecordRx(responseJson + "\\n");
                     // Parse response to check validity?
                     // Assuming presence of response means device is there.
                     return new Device(deviceId) { IsConnected = true, StatusMessage = "Online (Virtual)" };
                }
                return null;
            }

            // Real device logic
            try
            {
                // Clear buffers first
                if (_serialPort != null)
                {
                     _serialPort.DiscardInBuffer();
                     _serialPort.DiscardOutBuffer();
                }

                await SendCommandAsync(deviceId, "ping");
                
                await Task.Delay(50); 
                
                if (_serialPort != null && _serialPort.IsOpen && _serialPort.BytesToRead > 0)
                {
                    var response = _serialPort.ReadLine();
                    if (!string.IsNullOrWhiteSpace(response))
                    {
                        _trafficTap.RecordRx(response + "\\n");
                        // Ideally parse the JSON response here
                        return new Device(deviceId) { IsConnected = true, StatusMessage = "Online" };
                    }
                }
            }
            catch (Exception)
            {
                // Ignore timeouts
            }
            return null;
        }

        public async Task<IEnumerable<Device>> ScanDevicesAsync(int startId, int endId)
        {
            var foundDevices = new List<Device>();
            if (!IsConnected) return foundDevices;

            // Run in background to avoid blocking UI if called from UI thread
            return await Task.Run(async () =>
            {
                for (int id = startId; id <= endId; id++)
                {
                    var device = await PingDeviceAsync(id);
                    if (device != null)
                    {
                        foundDevices.Add(device);
                    }
                }
                return foundDevices;
            });
        }
    }
}
