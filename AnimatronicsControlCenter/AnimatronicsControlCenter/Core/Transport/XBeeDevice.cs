using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.Core.Transport;

/// <summary>
/// High-level XBee device abstraction
/// Combines SerialPort, FrameParser, and FrameBuilder
/// Implements IXBeeTransport for Fragment Protocol integration
/// </summary>
public class XBeeDevice : IXBeeTransport, IDisposable
{
    private readonly SerialPortWrapper _serial = new();
    private readonly ApiFrameParser _parser = new();
    private readonly ApiFrameBuilder _builder = new();

    // Pending TX Status responses
    private readonly ConcurrentDictionary<byte, TaskCompletionSource<TxStatusFrame>> _pendingTxStatus = new();

    // Pending AT Command responses
    private readonly ConcurrentDictionary<byte, TaskCompletionSource<AtCommandResponseFrame>> _pendingAtResponse = new();

    /// <summary>
    /// Device name for logging
    /// </summary>
    public string Name { get; set; } = "XBee";

    /// <summary>
    /// 64-bit address of this device (read from hardware)
    /// </summary>
    public ulong Address64 { get; private set; }

    /// <summary>
    /// Whether the device is connected
    /// </summary>
    public bool IsConnected => _serial.IsOpen;

    /// <summary>
    /// Event raised when RF data is received (0x90 RX Packet)
    /// WARNING: Always unsubscribe handlers when done to prevent memory leaks!
    /// </summary>
    public event Action<RxPacketFrame>? OnRxPacketReceived;

    /// <summary>
    /// IXBeeTransport implementation: RF data received event
    /// Parameters: (rfData, sourceAddress64)
    /// </summary>
    public event Action<byte[], ulong>? OnRfDataReceived;

    /// <summary>
    /// Clear all RF data event handlers (for cleanup)
    /// </summary>
    public void ClearRfDataHandlers()
    {
        OnRxPacketReceived = null;
        OnRfDataReceived = null;
    }

    /// <summary>
    /// Event raised on error
    /// </summary>
    public event Action<string>? OnError;

    /// <summary>
    /// Event raised for logging/debugging
    /// </summary>
    public event Action<string>? OnLog;

    /// <summary>
    /// Enable raw byte logging for debugging
    /// </summary>
    public bool EnableRawLogging { get; set; } = false;

    public XBeeDevice()
    {
        _serial.OnDataReceived += (data, offset, count) =>
        {
            if (EnableRawLogging)
            {
                var hex = BitConverter.ToString(data, offset, count).Replace("-", " ");
                OnLog?.Invoke($"[{Name}] RX RAW: {hex}");
            }
            _parser.ProcessBytes(data, offset, count);
        };

        _serial.OnError += msg => OnError?.Invoke($"[{Name}] {msg}");

        _parser.OnFrameReceived += HandleFrame;
        _parser.OnParseError += msg => OnError?.Invoke($"[{Name}] Parse: {msg}");
    }

    /// <summary>
    /// Connect to XBee device
    /// </summary>
    public async Task<bool> ConnectAsync(string portName, int baudRate = 115200)
    {
        if (!_serial.Open(portName, baudRate))
            return false;

        OnLog?.Invoke($"[{Name}] Serial port opened: {portName} @ {baudRate}");

        // Small delay to let XBee initialize
        await Task.Delay(100);

        // Read device 64-bit address (SH + SL)
        Address64 = await ReadAddress64Async();
        if (Address64 != 0)
        {
            OnLog?.Invoke($"[{Name}] Address: {Address64:X16}");
        }
        else
        {
            OnError?.Invoke($"[{Name}] Failed to read device address! Check AP=2 setting.");
        }

        return true;
    }

    /// <summary>
    /// Reset parser state (call between tests to clear any partial frames)
    /// </summary>
    public void ResetParser()
    {
        _parser.Reset();

        // Also clear any pending TX status/AT responses that might be stale
        foreach (var key in _pendingTxStatus.Keys.ToArray())
        {
            if (_pendingTxStatus.TryRemove(key, out var tcs))
            {
                tcs.TrySetCanceled();
            }
        }

        foreach (var key in _pendingAtResponse.Keys.ToArray())
        {
            if (_pendingAtResponse.TryRemove(key, out var tcs))
            {
                tcs.TrySetCanceled();
            }
        }
    }

    /// <summary>
    /// Clear serial port buffers (call between tests to remove stale data)
    /// </summary>
    public void ClearBuffers()
    {
        _serial.ClearBuffers();
    }

    /// <summary>
    /// Disconnect from XBee device
    /// </summary>
    public void Disconnect()
    {
        _serial.Close();
        _parser.Reset();
        OnLog?.Invoke($"[{Name}] Disconnected");
    }

    /// <summary>
    /// Send RF data to a destination address
    /// </summary>
    public async Task<TxStatusFrame?> SendDataAsync(ulong destAddress64, byte[] data,
        CancellationToken ct = default, int timeoutMs = 5000)
    {
        var request = new TxRequestFrame
        {
            DestAddress64 = destAddress64,
            RfData = data
        };

        var frame = _builder.BuildTxRequest(request);
        var tcs = new TaskCompletionSource<TxStatusFrame>();

        _pendingTxStatus[request.FrameId] = tcs;

        try
        {
            if (!_serial.Write(frame))
            {
                OnError?.Invoke($"[{Name}] Failed to write TX Request");
                return null;
            }

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                return await tcs.Task.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }
        finally
        {
            _pendingTxStatus.TryRemove(request.FrameId, out _);
        }
    }

    /// <summary>
    /// Send RF data without waiting for TX Status
    /// Implements IXBeeTransport.SendDataNoWait
    /// </summary>
    public bool SendDataNoWait(ulong destAddress64, byte[] data)
    {
        if (destAddress64 == 0)
        {
            OnError?.Invoke($"[{Name}] WARNING: Sending to address 0x0000000000000000!");
        }

        var request = new TxRequestFrame
        {
            FrameId = 0, // No response expected
            DestAddress64 = destAddress64,
            RfData = data
        };

        var frame = _builder.BuildTxRequest(request);

        if (EnableRawLogging)
        {
            var hex = BitConverter.ToString(frame).Replace("-", " ");
            OnLog?.Invoke($"[{Name}] TX RAW ({frame.Length}B): {hex.Substring(0, Math.Min(100, hex.Length))}...");
        }

        return _serial.Write(frame);
    }

    /// <summary>
    /// Handle incoming API frame
    /// </summary>
    private void HandleFrame(ApiFrame frame)
    {
        try
        {
            switch (frame.FrameType)
            {
                case ApiFrameType.RxPacket:
                    if (frame.RxPacket != null)
                    {
                        OnLog?.Invoke($"[{Name}] RX(0x90) from {frame.RxPacket.SourceAddress64:X16}, {frame.RxPacket.RfData.Length}B");
                        try
                        {
                            OnRxPacketReceived?.Invoke(frame.RxPacket);
                            // Also invoke IXBeeTransport event
                            OnRfDataReceived?.Invoke(frame.RxPacket.RfData, frame.RxPacket.SourceAddress64);
                        }
                        catch (Exception ex) { OnError?.Invoke($"[{Name}] RX handler error: {ex.Message}"); }
                    }
                    break;

                case ApiFrameType.ExplicitRxIndicator:
                    // Convert 0x91 Explicit RX to standard RX Packet for the application layer
                    if (frame.ExplicitRx != null)
                    {
                        OnLog?.Invoke($"[{Name}] RX(0x91) from {frame.ExplicitRx.SourceAddress64:X16}, {frame.ExplicitRx.RfData.Length}B");
                        var rxPacket = new RxPacketFrame
                        {
                            SourceAddress64 = frame.ExplicitRx.SourceAddress64,
                            SourceAddress16 = frame.ExplicitRx.SourceAddress16,
                            ReceiveOptions = frame.ExplicitRx.ReceiveOptions,
                            RfData = frame.ExplicitRx.RfData
                        };
                        try
                        {
                            OnRxPacketReceived?.Invoke(rxPacket);
                            // Also invoke IXBeeTransport event
                            OnRfDataReceived?.Invoke(rxPacket.RfData, rxPacket.SourceAddress64);
                        }
                        catch (Exception ex) { OnError?.Invoke($"[{Name}] RX handler error: {ex.Message}"); }
                    }
                    break;

                case ApiFrameType.TxStatus:
                    if (frame.TxStatus != null)
                    {
                        OnLog?.Invoke($"[{Name}] TX Status: {frame.TxStatus.DeliveryStatus}");
                        if (_pendingTxStatus.TryRemove(frame.TxStatus.FrameId, out var tcs))
                        {
                            tcs.TrySetResult(frame.TxStatus);
                        }
                    }
                    break;

                case ApiFrameType.AtCommandResponse:
                    if (frame.AtResponse != null)
                    {
                        OnLog?.Invoke($"[{Name}] AT Response: {frame.AtResponse.Command} = {(frame.AtResponse.IsSuccess ? "OK" : "FAIL")}");
                        if (_pendingAtResponse.TryRemove(frame.AtResponse.FrameId, out var atTcs))
                        {
                            atTcs.TrySetResult(frame.AtResponse);
                        }
                    }
                    break;
            }
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"[{Name}] Frame handling error: {ex.Message}");
        }
    }

    /// <summary>
    /// Send AT command and wait for response
    /// </summary>
    public async Task<AtCommandResponseFrame?> SendAtCommandAsync(string command, byte[]? parameter = null,
        CancellationToken ct = default, int timeoutMs = 2000)
    {
        var frameId = _builder.GetNextFrameId();
        var frame = _builder.BuildAtCommand(frameId, command, parameter);
        var tcs = new TaskCompletionSource<AtCommandResponseFrame>();

        _pendingAtResponse[frameId] = tcs;

        OnLog?.Invoke($"[{Name}] Sending AT command: {command} (frameId={frameId})");

        if (EnableRawLogging)
        {
            var hex = BitConverter.ToString(frame).Replace("-", " ");
            OnLog?.Invoke($"[{Name}] AT TX RAW: {hex}");
        }

        try
        {
            if (!_serial.Write(frame))
            {
                OnError?.Invoke($"[{Name}] Failed to write AT command: {command}");
                return null;
            }

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                return await tcs.Task.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                OnError?.Invoke($"[{Name}] AT command timeout: {command}");
                return null;
            }
        }
        finally
        {
            _pendingAtResponse.TryRemove(frameId, out _);
        }
    }

    /// <summary>
    /// Read 64-bit address from device (SH << 32 | SL)
    /// </summary>
    private async Task<ulong> ReadAddress64Async()
    {
        // Read SH (Serial Number High)
        var shResponse = await SendAtCommandAsync("SH");
        if (shResponse == null || !shResponse.IsSuccess || shResponse.Data.Length < 4)
        {
            OnError?.Invoke($"[{Name}] Failed to read SH");
            return 0;
        }

        // Read SL (Serial Number Low)
        var slResponse = await SendAtCommandAsync("SL");
        if (slResponse == null || !slResponse.IsSuccess || slResponse.Data.Length < 4)
        {
            OnError?.Invoke($"[{Name}] Failed to read SL");
            return 0;
        }

        // Combine SH and SL into 64-bit address
        ulong sh = ((ulong)shResponse.Data[0] << 24) |
                   ((ulong)shResponse.Data[1] << 16) |
                   ((ulong)shResponse.Data[2] << 8) |
                   shResponse.Data[3];

        ulong sl = ((ulong)slResponse.Data[0] << 24) |
                   ((ulong)slResponse.Data[1] << 16) |
                   ((ulong)slResponse.Data[2] << 8) |
                   slResponse.Data[3];

        return (sh << 32) | sl;
    }

    public void Dispose()
    {
        Disconnect();
        _serial.Dispose();
        GC.SuppressFinalize(this);
    }
}
