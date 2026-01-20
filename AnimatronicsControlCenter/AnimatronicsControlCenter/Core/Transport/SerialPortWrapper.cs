using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace AnimatronicsControlCenter.Core.Transport;

/// <summary>
/// Thread-safe wrapper for SerialPort with async operations
/// </summary>
public class SerialPortWrapper : IDisposable
{
    private SerialPort? _port;
    private readonly object _lock = new();
    private CancellationTokenSource? _readCts;
    private Task? _readTask;

    /// <summary>
    /// Event raised when data is received
    /// </summary>
    public event Action<byte[], int, int>? OnDataReceived;

    /// <summary>
    /// Event raised on error
    /// </summary>
    public event Action<string>? OnError;

    /// <summary>
    /// Port name (e.g., "COM3")
    /// </summary>
    public string PortName { get; private set; } = "";

    /// <summary>
    /// Whether the port is currently open
    /// </summary>
    public bool IsOpen => _port?.IsOpen ?? false;

    /// <summary>
    /// Open the serial port
    /// </summary>
    public bool Open(string portName, int baudRate = 115200)
    {
        lock (_lock)
        {
            try
            {
                Close();

                _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 100,
                    WriteTimeout = 2000,
                    ReadBufferSize = 16384,  // Increased for high-throughput
                    WriteBufferSize = 16384  // Increased for high-throughput
                };

                _port.Open();
                PortName = portName;

                // Start background read task
                _readCts = new CancellationTokenSource();
                _readTask = Task.Run(() => ReadLoop(_readCts.Token));

                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to open {portName}: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Close the serial port
    /// </summary>
    public void Close()
    {
        lock (_lock)
        {
            _readCts?.Cancel();
            _readTask?.Wait(500);
            _readCts?.Dispose();
            _readCts = null;
            _readTask = null;

            if (_port != null)
            {
                try
                {
                    if (_port.IsOpen)
                        _port.Close();
                }
                catch { }
                _port.Dispose();
                _port = null;
            }
        }
    }

    /// <summary>
    /// Write data to the serial port
    /// </summary>
    public bool Write(byte[] data)
    {
        lock (_lock)
        {
            if (_port == null || !_port.IsOpen)
                return false;

            try
            {
                _port.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Write error: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Clear both input and output buffers
    /// Call between tests to ensure no stale data remains
    /// </summary>
    public void ClearBuffers()
    {
        lock (_lock)
        {
            if (_port == null || !_port.IsOpen)
                return;

            try
            {
                _port.DiscardInBuffer();
                _port.DiscardOutBuffer();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Buffer clear error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Background read loop
    /// </summary>
    private void ReadLoop(CancellationToken ct)
    {
        var buffer = new byte[1024];

        while (!ct.IsCancellationRequested)
        {
            try
            {
                SerialPort? port;
                lock (_lock)
                {
                    port = _port;
                }

                if (port == null || !port.IsOpen)
                {
                    Thread.Sleep(10);
                    continue;
                }

                int bytesToRead = port.BytesToRead;
                if (bytesToRead > 0)
                {
                    int toRead = Math.Min(bytesToRead, buffer.Length);
                    int read = port.Read(buffer, 0, toRead);
                    if (read > 0)
                    {
                        try
                        {
                            OnDataReceived?.Invoke(buffer, 0, read);
                        }
                        catch (Exception ex)
                        {
                            OnError?.Invoke($"Data processing error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (TimeoutException)
            {
                // Normal timeout, continue
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                {
                    OnError?.Invoke($"Read error: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
        }
    }

    /// <summary>
    /// Get available COM port names
    /// </summary>
    public static string[] GetPortNames()
    {
        try
        {
            return SerialPort.GetPortNames();
        }
        catch
        {
            return [];
        }
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }
}
