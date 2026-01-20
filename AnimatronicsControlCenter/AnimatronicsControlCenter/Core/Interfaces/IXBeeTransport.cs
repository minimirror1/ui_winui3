using System;

namespace AnimatronicsControlCenter.Core.Interfaces;

/// <summary>
/// Interface for XBee RF transport layer
/// Abstracts XBee device communication for Fragment Protocol
/// </summary>
public interface IXBeeTransport
{
    /// <summary>
    /// Whether the XBee device is connected
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 64-bit address of this XBee device
    /// </summary>
    ulong Address64 { get; }

    /// <summary>
    /// Send RF data without waiting for TX status
    /// </summary>
    /// <param name="destAddress64">Destination 64-bit address</param>
    /// <param name="data">Data to send</param>
    /// <returns>True if data was queued for transmission</returns>
    bool SendDataNoWait(ulong destAddress64, byte[] data);

    /// <summary>
    /// Event raised when RF data is received
    /// Parameters: (rfData, sourceAddress64)
    /// </summary>
    event Action<byte[], ulong>? OnRfDataReceived;

    /// <summary>
    /// Event raised for logging/debugging
    /// </summary>
    event Action<string>? OnLog;

    /// <summary>
    /// Event raised on error
    /// </summary>
    event Action<string>? OnError;
}
