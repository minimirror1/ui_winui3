using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Link;
using AnimatronicsControlCenter.Core.Transport;
using Microsoft.UI.Dispatching;

namespace AnimatronicsControlCenter.Infrastructure;

/// <summary>
/// High-level service for XBee DigiMesh communication with Fragment Protocol support
/// </summary>
public class XBeeService : IDisposable
{
    private readonly XBeeDevice _device;
    private readonly SessionManager _sessionManager;
    private readonly FragmentReceiver _receiver;
    private readonly FragmentTransmitter _transmitter;
    private DispatcherQueue? _dispatcherQueue;
    private bool _disposed;

    /// <summary>
    /// Whether the XBee device is connected
    /// </summary>
    public bool IsConnected => _device.IsConnected;

    /// <summary>
    /// 64-bit address of this XBee device
    /// </summary>
    public ulong Address64 => _device.Address64;

    /// <summary>
    /// Event raised when a complete message is received from a remote device
    /// Parameters: (messageData, sourceAddress64)
    /// Called on UI thread when DispatcherQueue is set
    /// </summary>
    public event Action<byte[], ulong>? OnMessageReceived;

    /// <summary>
    /// Event raised when fragment is received (for sliding timeout)
    /// </summary>
    public event Action? OnFragmentActivity;

    /// <summary>
    /// Event raised for log messages
    /// </summary>
    public event Action<string>? OnLog;

    /// <summary>
    /// Event raised on error
    /// </summary>
    public event Action<string>? OnError;

    /// <summary>
    /// Statistics
    /// </summary>
    public int TotalFragmentsSent => _transmitter.TotalFragmentsSent;
    public int TotalFragmentsReceived => _receiver.TotalFragmentsReceived;
    public int RetransmittedFragments => _transmitter.RetransmittedFragments;
    public int NacksSent => _receiver.NacksSent;
    public int CrcFailures => _receiver.CrcFailures;
    public int MessagesCompleted => _receiver.MessagesCompleted;

    public XBeeService()
    {
        _device = new XBeeDevice();
        _sessionManager = new SessionManager();
        _receiver = new FragmentReceiver(_device, _sessionManager);
        _transmitter = new FragmentTransmitter(_device, _sessionManager);

        // Wire up events
        _device.OnLog += msg => OnLog?.Invoke(msg);
        _device.OnError += msg => OnError?.Invoke(msg);

        _receiver.OnLog += msg => OnLog?.Invoke(msg);
        _transmitter.OnLog += msg => OnLog?.Invoke(msg);

        // Connect NACK/DONE handling between receiver and transmitter
        _receiver.OnNackReceived += (nack, addr) => _transmitter.HandleNack(nack, addr);
        _receiver.OnDoneReceived += msgId => _transmitter.HandleDone(msgId);

        // Connect RF data to fragment receiver
        _device.OnRfDataReceived += (data, sourceAddr) => _receiver.ProcessRfData(data, sourceAddr);

        // Connect complete message to user callback
        _receiver.OnMessageReceived += HandleCompleteMessage;

        // Forward fragment progress for sliding timeout
        _receiver.OnFragmentProgress += (msgId, fragIdx, fragCnt) =>
        {
            OnFragmentActivity?.Invoke();
        };
    }

    /// <summary>
    /// Set dispatcher queue for UI thread callbacks
    /// </summary>
    public void SetDispatcherQueue(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
    }

    /// <summary>
    /// Connect to XBee device on specified COM port
    /// </summary>
    public async Task<bool> ConnectAsync(string portName, int baudRate = 115200)
    {
        return await _device.ConnectAsync(portName, baudRate);
    }

    /// <summary>
    /// Disconnect from XBee device
    /// </summary>
    public void Disconnect()
    {
        _device.Disconnect();
        _sessionManager.ClearAllSessions();
    }

    /// <summary>
    /// Send a message to a remote XBee device
    /// Message will be fragmented if larger than 30 bytes
    /// </summary>
    /// <param name="data">Message data to send</param>
    /// <param name="destAddress64">Destination 64-bit XBee address</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if message was sent and acknowledged (DONE received)</returns>
    public async Task<bool> SendMessageAsync(byte[] data, ulong destAddress64, CancellationToken ct = default)
    {
        return await _transmitter.SendMessageAsync(data, destAddress64, ct);
    }

    /// <summary>
    /// Send a string message to a remote XBee device (UTF-8 encoded)
    /// </summary>
    public async Task<bool> SendMessageAsync(string message, ulong destAddress64, CancellationToken ct = default)
    {
        var data = Encoding.UTF8.GetBytes(message);
        return await _transmitter.SendMessageAsync(data, destAddress64, ct);
    }

    /// <summary>
    /// Get available COM port names
    /// </summary>
    public static string[] GetPortNames()
    {
        return SerialPortWrapper.GetPortNames();
    }

    /// <summary>
    /// Reset all statistics
    /// </summary>
    public void ResetStats()
    {
        _receiver.ResetStats();
        _transmitter.ResetStats();
    }

    /// <summary>
    /// Get current session counts for debugging
    /// </summary>
    public (int TxCount, int RxCount) GetSessionCounts()
    {
        return _sessionManager.GetSessionCounts();
    }

    /// <summary>
    /// Handle complete message from receiver - dispatch to UI thread if available
    /// </summary>
    private void HandleCompleteMessage(byte[] data, ulong sourceAddress)
    {
        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                OnMessageReceived?.Invoke(data, sourceAddress);
            });
        }
        else
        {
            OnMessageReceived?.Invoke(data, sourceAddress);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _receiver.Dispose();
        _sessionManager.Dispose();
        _device.Dispose();

        GC.SuppressFinalize(this);
    }
}
