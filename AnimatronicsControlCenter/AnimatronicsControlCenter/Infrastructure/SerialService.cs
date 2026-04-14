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

        // PING은 짧은 타임아웃 적용
        private static readonly HashSet<BinaryCommand> ShortTimeoutCommands = new()
        {
            BinaryCommand.Ping
        };

        // 대용량 페이로드 명령은 Fragment Protocol 기본 타임아웃 사용
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
        private bool _isVirtualConnected;

        // Binary 응답 매칭: key = "{deviceId}:{cmd_byte}"
        private readonly ConcurrentDictionary<string, TaskCompletionSource<byte[]>> _pendingResponses = new();

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

            var success = await _xbeeService.ConnectAsync(portName, baudRate);
            if (!success)
                throw new InvalidOperationException($"Failed to connect to XBee device on {portName}");
        }

        public void Disconnect()
        {
            _isVirtualConnected = false;
            if (_xbeeService.IsConnected)
                _xbeeService.Disconnect();
        }

        // ── Binary 전송 (Fire-and-forget) ────────────────────────────────

        public async Task SendBinaryCommandAsync(int deviceId, byte[] packet)
        {
            if (!IsConnected) return;

            _trafficTap.RecordTxBytes(packet);

            if (_settingsService.IsVirtualModeEnabled)
            {
                _virtualDeviceManager.ProcessBinaryCommand(packet);
                await Task.CompletedTask;
                return;
            }

            var broadcastAddress = ApiConstants.BroadcastAddress64;
            using var cts = new CancellationTokenSource(GetSessionTimeoutMs(GetCmdFromPacket(packet)));
            await _xbeeService.SendMessageAsync(packet, broadcastAddress, cts.Token);
        }

        // ── Binary 쿼리 (응답 대기) ──────────────────────────────────────

        // payload 없는 no-arg 쿼리 편의 메서드
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, BinaryCommand cmd)
            => SendBinaryQueryAsync(deviceId, cmd, BuildRequestPacket((byte)deviceId, cmd));

        public async Task<byte[]?> SendBinaryQueryAsync(int deviceId, BinaryCommand cmd, byte[] packet)
        {
            if (!IsConnected) return null;
            _trafficTap.RecordTxBytes(packet);

            if (_settingsService.IsVirtualModeEnabled)
            {
                await Task.Delay(20);
                var response = _virtualDeviceManager.ProcessBinaryCommand(packet);
                if (response != null)
                    _trafficTap.RecordRxBytes(response);
                return response;
            }

            // Real device: pending 등록 후 전송
            // Ping 요청의 응답은 Pong cmd로 오므로 key는 Ping(0x01)로 등록
            var responseKey = $"{deviceId}:{(byte)cmd}";
            var tcs = new TaskCompletionSource<byte[]>();
            _pendingResponses[responseKey] = tcs;

            try
            {
                var broadcastAddress = ApiConstants.BroadcastAddress64;
                using var sendCts = new CancellationTokenSource(GetSessionTimeoutMs(cmd));
                var sendSuccess = await _xbeeService.SendMessageAsync(packet, broadcastAddress, sendCts.Token);

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

                    var result = await tcs.Task.WaitAsync(timeoutCts.Token);
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
        }

        // ── PingDeviceAsync ──────────────────────────────────────────────

        public async Task<Device?> PingDeviceAsync(int deviceId)
        {
            if (!IsConnected) return null;

            if (_settingsService.IsVirtualModeEnabled)
            {
                await Task.Delay(20);
                var ping = BinarySerializer.EncodePing(BinaryProtocolConst.HostId, (byte)deviceId);
                _trafficTap.RecordTxBytes(ping);
                var resp = _virtualDeviceManager.ProcessBinaryCommand(ping);
                if (resp != null && BinaryDeserializer.TryParseResponseHeader(resp, out var hdr)
                    && hdr.Cmd == BinaryCommand.Pong && hdr.Status == ResponseStatus.Ok)
                {
                    _trafficTap.RecordRxBytes(resp);
                    return new Device(deviceId) { IsConnected = true, StatusMessage = "Online (Virtual)" };
                }
                return null;
            }

            try
            {
                var response = await SendBinaryQueryAsync(deviceId, BinaryCommand.Ping);
                if (response != null
                    && BinaryDeserializer.TryParseResponseHeader(response, out var hdr)
                    && hdr.Cmd == BinaryCommand.Pong && hdr.Status == ResponseStatus.Ok)
                {
                    return new Device(deviceId) { IsConnected = true, StatusMessage = "Online" };
                }
            }
            catch (Exception)
            {
                // 타임아웃 무시
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
                    var device = await PingDeviceAsync(id);
                    if (device != null)
                        foundDevices.Add(device);
                }
                return foundDevices;
            });
        }

        // ── Binary 수신 처리 ─────────────────────────────────────────────

        private void HandleBinaryReceived(byte[] data, ulong sourceAddress)
        {
            try
            {
                if (!BinaryDeserializer.TryParseResponseHeader(data, out var hdr)) return;

                _trafficTap.RecordRxBytes(data);

                // PONG 응답은 PING 요청 key로 매칭
                var lookupCmd  = hdr.Cmd == BinaryCommand.Pong ? BinaryCommand.Ping : hdr.Cmd;
                var responseKey = $"{hdr.SrcId}:{(byte)lookupCmd}";

                if (_pendingResponses.TryRemove(responseKey, out var tcs))
                {
                    if (!tcs.Task.IsCompleted)
                        tcs.TrySetResult(data);
                }
            }
            catch (Exception)
            {
                // 파싱 오류 무시
            }
        }

        public void Dispose()
        {
            _xbeeService.OnMessageReceived -= HandleBinaryReceived;
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────

        private static byte[] BuildRequestPacket(byte tarId, BinaryCommand cmd)
        {
            return cmd switch
            {
                BinaryCommand.Ping          => BinarySerializer.EncodePing(BinaryProtocolConst.HostId, tarId),
                BinaryCommand.GetMotors     => BinarySerializer.EncodeGetMotors(BinaryProtocolConst.HostId, tarId),
                BinaryCommand.GetMotorState => BinarySerializer.EncodeGetMotorState(BinaryProtocolConst.HostId, tarId),
                BinaryCommand.GetFiles      => BinarySerializer.EncodeGetFiles(BinaryProtocolConst.HostId, tarId),
                _ => throw new ArgumentException($"Cannot build no-payload packet for cmd={cmd}. Use SendBinaryCommandAsync with pre-built packet."),
            };
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
