using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Link;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Transport;

namespace AnimatronicsControlCenter.Infrastructure
{
    public class SerialService : ISerialService, IDisposable
    {
        private const byte HostId = 0;
        private const byte BroadcastId = 255;

        private readonly ISettingsService _settingsService;
        private readonly ISerialTrafficTap _trafficTap;
        private readonly VirtualDeviceManager _virtualDeviceManager;
        private readonly XBeeService _xbeeService;
        private bool _isVirtualConnected;

        // Response matching for SendQueryAsync and PingDeviceAsync
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingResponses = new();

        public SerialService(ISettingsService settingsService, ISerialTrafficTap trafficTap, XBeeService xbeeService)
        {
            _settingsService = settingsService;
            _trafficTap = trafficTap;
            _virtualDeviceManager = new VirtualDeviceManager();
            _xbeeService = xbeeService;

            // Subscribe to XBeeService message received events
            _xbeeService.OnMessageReceived += HandleMessageReceived;
        }

        public bool IsConnected
        {
            get
            {
                if (_settingsService.IsVirtualModeEnabled)
                    return _isVirtualConnected;
                return _xbeeService.IsConnected;
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

            if (_xbeeService.IsConnected)
                Disconnect();

            var success = await _xbeeService.ConnectAsync(portName, baudRate);
            if (!success)
            {
                throw new InvalidOperationException($"Failed to connect to XBee device on {portName}");
            }
        }

        public void Disconnect()
        {
            _isVirtualConnected = false;

            if (_xbeeService.IsConnected)
            {
                _xbeeService.Disconnect();
            }
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

            // Convert JSON to UTF-8 bytes and send via Fragment Protocol
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var broadcastAddress = ApiConstants.BroadcastAddress64;
            
            using var cts = new CancellationTokenSource(FragmentProtocol.SessionTimeoutMs);
            await _xbeeService.SendMessageAsync(jsonBytes, broadcastAddress, cts.Token);
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

            // Create response key for matching
            var responseKey = $"{deviceId}:{command}";
            var tcs = new TaskCompletionSource<string>();
            _pendingResponses[responseKey] = tcs;

            try
            {
                // Convert JSON to UTF-8 bytes and send via Fragment Protocol
                var jsonBytes = Encoding.UTF8.GetBytes(json);
                var broadcastAddress = ApiConstants.BroadcastAddress64;

                using var sendCts = new CancellationTokenSource(FragmentProtocol.SessionTimeoutMs);
                var sendSuccess = await _xbeeService.SendMessageAsync(jsonBytes, broadcastAddress, sendCts.Token);

                if (!sendSuccess)
                {
                    _pendingResponses.TryRemove(responseKey, out _);
                    return null;
                }

                // Sliding timeout implementation
                var timeout = TimeSpan.FromSeconds(_settingsService.ResponseTimeoutSeconds);
                using var timeoutCts = new CancellationTokenSource(timeout);

                // Reset timeout on fragment activity (sliding timeout)
                void ResetTimeout()
                {
                    try
                    {
                        timeoutCts.CancelAfter(timeout);
                    }
                    catch (ObjectDisposedException)
                    {
                        // CancellationTokenSource already disposed, ignore
                    }
                }

                _xbeeService.OnFragmentActivity += ResetTimeout;

                try
                {
                    timeoutCts.Token.Register(() =>
                    {
                        // Remove from pending responses on timeout
                        _pendingResponses.TryRemove(responseKey, out _);
                        tcs.TrySetCanceled();
                    });

                    var response = await tcs.Task.WaitAsync(timeoutCts.Token);
                    // Successfully received response, remove from pending
                    _pendingResponses.TryRemove(responseKey, out _);
                    return response;
                }
                catch (OperationCanceledException)
                {
                    // Timeout occurred, already removed from _pendingResponses in Register callback
                    return null;
                }
                finally
                {
                    _xbeeService.OnFragmentActivity -= ResetTimeout;
                }
            }
            catch
            {
                _pendingResponses.TryRemove(responseKey, out _);
                return null;
            }
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

            // Real device logic - use SendQueryAsync to wait for response
            try
            {
                var response = await SendQueryAsync(deviceId, "ping");
                if (!string.IsNullOrWhiteSpace(response))
                {
                    // Parse response to check validity
                    var json = JsonNode.Parse(response);
                    if (json != null && json["status"]?.ToString() == "ok")
                    {
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

        /// <summary>
        /// Handle messages received from XBeeService
        /// Matches responses to pending queries by parsing JSON and checking src_id and cmd
        /// Note: Some commands may have different response cmd (e.g., "ping" -> "pong")
        /// </summary>
        private void HandleMessageReceived(byte[] data, ulong sourceAddress)
        {
            try
            {
                // Convert bytes to JSON string
                var json = Encoding.UTF8.GetString(data);
                _trafficTap.RecordRx(json + "\\n");

                // Parse JSON to extract src_id and cmd
                var jsonNode = JsonNode.Parse(json);
                if (jsonNode == null) return;

                var srcId = jsonNode["src_id"]?.GetValue<int>();
                var cmd = jsonNode["cmd"]?.GetValue<string>();
                
                if (!srcId.HasValue || string.IsNullOrEmpty(cmd)) return;

                // Try to match with pending responses
                // Format: "{deviceId}:{command}"
                // First try exact match
                var responseKey = $"{srcId.Value}:{cmd}";
                
                if (_pendingResponses.TryRemove(responseKey, out var tcs))
                {
                    // Check if task is already completed/cancelled before setting result
                    if (!tcs.Task.IsCompleted)
                    {
                        tcs.TrySetResult(json);
                    }
                    return;
                }

                // Handle special cases where response cmd differs from request cmd
                // e.g., "ping" request might have "pong" response
                if (cmd == "pong")
                {
                    responseKey = $"{srcId.Value}:ping";
                    if (_pendingResponses.TryRemove(responseKey, out tcs))
                    {
                        if (!tcs.Task.IsCompleted)
                        {
                            tcs.TrySetResult(json);
                        }
                        return;
                    }
                }
            }
            catch (Exception)
            {
                // Ignore parsing errors
            }
        }

        public void Dispose()
        {
            _xbeeService.OnMessageReceived -= HandleMessageReceived;
        }
    }
}
