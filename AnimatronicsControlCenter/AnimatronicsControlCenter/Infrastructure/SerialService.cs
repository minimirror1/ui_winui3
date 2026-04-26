using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Link;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Protocol;
using AnimatronicsControlCenter.Core.Transport;

namespace AnimatronicsControlCenter.Infrastructure
{
    public class SerialService : ISerialService, IDisposable
    {
        private const int ShortSessionTimeoutMs = 2000;

        private static readonly HashSet<BinaryCommand> ShortTimeoutCommands = new()
        {
            BinaryCommand.Ping
        };

        private static readonly HashSet<BinaryCommand> LongTimeoutCommands = new()
        {
            BinaryCommand.SaveFile,
            BinaryCommand.GetFile,
            BinaryCommand.VerifyFile,
            BinaryCommand.GetFiles,
        };

        private readonly ISettingsService _settingsService;
        private readonly ISerialTrafficTap _trafficTap;
        private readonly VirtualDeviceManager _virtualDeviceManager;
        private readonly XBeeService _xbeeService;
        private readonly DeviceCommandGate _deviceCommandGate = new();
        private bool _isVirtualConnected;

        private readonly ConcurrentDictionary<string, TaskCompletionSource<ReceivedBinaryResponse?>> _pendingResponses = new();

        private readonly record struct ReceivedBinaryResponse(byte[] Data, ulong SourceAddress);

        public SerialService(ISettingsService settingsService, ISerialTrafficTap trafficTap, XBeeService xbeeService)
        {
            _settingsService = settingsService;
            _trafficTap = trafficTap;
            _virtualDeviceManager = new VirtualDeviceManager();
            _xbeeService = xbeeService;

            _xbeeService.OnMessageReceived += HandleBinaryReceived;
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

            var success = await _xbeeService.ConnectAsync(portName, baudRate).ConfigureAwait(false);
            if (!success)
                throw new InvalidOperationException($"Failed to connect to XBee device on {portName}");
        }

        public void Disconnect()
        {
            _isVirtualConnected = false;
            if (_xbeeService.IsConnected)
                _xbeeService.Disconnect();
        }

        public async Task SendBinaryCommandAsync(int deviceId, byte[] packet)
        {
            if (!IsConnected) return;

            await _deviceCommandGate.RunExclusiveAsync(deviceId, async () =>
            {
                _trafficTap.RecordTxBytes(packet);

                if (_settingsService.IsVirtualModeEnabled)
                {
                    _virtualDeviceManager.ProcessBinaryCommand(packet);
                    await Task.CompletedTask;
                    return;
                }

                var broadcastAddress = ApiConstants.BroadcastAddress64;
                using var cts = new CancellationTokenSource(GetSessionTimeoutMs(GetCmdFromPacket(packet)));
                await _xbeeService.SendMessageAsync(packet, broadcastAddress, cts.Token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, BinaryCommand cmd)
            => SendBinaryQueryAsync(deviceId, cmd, BuildRequestPacket((byte)deviceId, cmd));

        public async Task<byte[]?> SendBinaryQueryAsync(int deviceId, BinaryCommand cmd, byte[] packet)
        {
            var response = await SendBinaryQueryWithSourceAsync(deviceId, cmd, packet).ConfigureAwait(false);
            return response?.Data;
        }

        private async Task<ReceivedBinaryResponse?> SendBinaryQueryWithSourceAsync(int deviceId, BinaryCommand cmd, byte[] packet)
        {
            if (!IsConnected) return null;

            return await _deviceCommandGate.RunExclusiveAsync<ReceivedBinaryResponse?>(deviceId, async () =>
            {
                _trafficTap.RecordTxBytes(packet);

                if (_settingsService.IsVirtualModeEnabled)
                {
                    await Task.Delay(20).ConfigureAwait(false);
                    var response = _virtualDeviceManager.ProcessBinaryCommand(packet);
                    if (response != null)
                    {
                        _trafficTap.RecordRxBytes(response);
                        return new ReceivedBinaryResponse(response, 0);
                    }

                    return null;
                }

                var responseKey = $"{deviceId}:{(byte)cmd}";
                var tcs = new TaskCompletionSource<ReceivedBinaryResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pendingResponses[responseKey] = tcs;

                try
                {
                    var broadcastAddress = ApiConstants.BroadcastAddress64;
                    using var sendCts = new CancellationTokenSource(GetSessionTimeoutMs(cmd));
                    var sendSuccess = await _xbeeService.SendMessageAsync(packet, broadcastAddress, sendCts.Token).ConfigureAwait(false);

                    if (!sendSuccess)
                    {
                        _pendingResponses.TryRemove(responseKey, out _);
                        return null;
                    }

                    var timeout = TimeSpan.FromSeconds(_settingsService.ResponseTimeoutSeconds);
                    using var timeoutCts = new CancellationTokenSource(timeout);

                    void ResetTimeout()
                    {
                        try { timeoutCts.CancelAfter(timeout); }
                        catch (ObjectDisposedException) { }
                    }

                    _xbeeService.OnFragmentActivity += ResetTimeout;

                    try
                    {
                        timeoutCts.Token.Register(() =>
                        {
                            _pendingResponses.TryRemove(responseKey, out _);
                            tcs.TrySetCanceled();
                        });

                        var result = await tcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
                        _pendingResponses.TryRemove(responseKey, out _);
                        return result;
                    }
                    catch (OperationCanceledException)
                    {
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
            }).ConfigureAwait(false);
        }

        public async Task<Device?> PingDeviceAsync(int deviceId)
        {
            if (!IsConnected) return null;

            try
            {
                var packet = BuildRequestPacket((byte)deviceId, BinaryCommand.Ping);
                var response = await SendBinaryQueryWithSourceAsync(deviceId, BinaryCommand.Ping, packet).ConfigureAwait(false);
                if (response == null)
                    return null;

                return CreateDeviceFromPong(deviceId, response.Value, _settingsService.IsVirtualModeEnabled);
            }
            catch (Exception)
            {
            }

            return null;
        }

        public async Task<IEnumerable<Device>> ScanDevicesAsync(int startId, int endId)
        {
            var foundDevices = new List<Device>();
            if (!IsConnected) return foundDevices;

            return await Task.Run(async () =>
            {
                for (int id = startId; id <= endId; id++)
                {
                    var device = await PingDeviceAsync(id).ConfigureAwait(false);
                    if (device != null)
                        foundDevices.Add(device);
                }

                return foundDevices;
            }).ConfigureAwait(false);
        }

        private void HandleBinaryReceived(byte[] data, ulong sourceAddress)
        {
            try
            {
                if (!BinaryDeserializer.TryParseResponseHeader(data, out var hdr)) return;

                _trafficTap.RecordRxBytes(data);

                var lookupCmd = hdr.Cmd == BinaryCommand.Pong ? BinaryCommand.Ping : hdr.Cmd;
                var responseKey = $"{hdr.SrcId}:{(byte)lookupCmd}";

                if (_pendingResponses.TryRemove(responseKey, out var tcs) && !tcs.Task.IsCompleted)
                    tcs.TrySetResult(new ReceivedBinaryResponse(data, sourceAddress));
            }
            catch (Exception)
            {
            }
        }

        private static Device? CreateDeviceFromPong(int deviceId, ReceivedBinaryResponse response, bool isVirtual)
        {
            if (!BinaryDeserializer.TryParseResponseHeader(response.Data, out var hdr) ||
                hdr.Cmd != BinaryCommand.Pong ||
                hdr.Status != ResponseStatus.Ok)
            {
                return null;
            }

            int payloadStart = BinaryProtocolConst.ResponseHeaderSize;
            if (response.Data.Length < payloadStart + hdr.PayloadLen)
                return null;

            var payload = response.Data.AsSpan(payloadStart, hdr.PayloadLen);
            if (!BinaryDeserializer.TryParsePongResponse(payload, out var pongStatus))
                return null;

            var device = new Device(deviceId);
            FirmwareStatusProjection.Apply(device, pongStatus, response.SourceAddress, isVirtual);
            return device;
        }

        public void Dispose()
        {
            _xbeeService.OnMessageReceived -= HandleBinaryReceived;
        }

        private byte[] BuildRequestPacket(byte tarId, BinaryCommand cmd)
        {
            return cmd switch
            {
                BinaryCommand.Ping => BuildTimedPingRequestPacket(tarId),
                BinaryCommand.GetMotors => BinarySerializer.EncodeGetMotors(BinaryProtocolConst.HostId, tarId),
                BinaryCommand.GetMotorState => BinarySerializer.EncodeGetMotorState(BinaryProtocolConst.HostId, tarId),
                BinaryCommand.GetFiles => BinarySerializer.EncodeGetFiles(BinaryProtocolConst.HostId, tarId),
                _ => throw new ArgumentException($"Cannot build no-payload packet for cmd={cmd}. Use SendBinaryCommandAsync with pre-built packet."),
            };
        }

        private byte[] BuildTimedPingRequestPacket(byte tarId)
        {
            return BinarySerializer.EncodePing(
                BinaryProtocolConst.HostId,
                tarId,
                PingTimePayloadFactory.Create(
                    _settingsService.PingCountryCode,
                    _settingsService.PingUtcOffsetMinutes,
                    DateTimeOffset.UtcNow));
        }

        private static BinaryCommand GetCmdFromPacket(byte[] packet)
        {
            if (packet.Length < 3) return BinaryCommand.Error;
            return (BinaryCommand)packet[2];
        }

        private static int GetSessionTimeoutMs(BinaryCommand cmd)
        {
            if (ShortTimeoutCommands.Contains(cmd)) return ShortSessionTimeoutMs;
            return FragmentProtocol.SessionTimeoutMs;
        }
    }
}
