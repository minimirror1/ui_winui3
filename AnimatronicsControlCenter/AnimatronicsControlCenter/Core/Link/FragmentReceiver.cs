using System;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.Core.Link;

/// <summary>
/// Handles fragment reception and reassembly with NACK generation
/// </summary>
public class FragmentReceiver : IDisposable
{
    private readonly IXBeeTransport _transport;
    private readonly SessionManager _sessionManager;
    private bool _disposed;

    /// <summary>
    /// Event raised when a complete message is received
    /// </summary>
    public event Action<byte[], ulong>? OnMessageReceived;

    /// <summary>
    /// Event raised when NACK is sent
    /// </summary>
    public event Action<ushort, ushort[]>? OnNackSent;

    /// <summary>
    /// Event for logging
    /// </summary>
    public event Action<string>? OnLog;

    /// <summary>
    /// Statistics
    /// </summary>
    public int TotalFragmentsReceived { get; private set; }
    public int CrcFailures { get; private set; }
    public int NacksSent { get; private set; }
    public int MessagesCompleted { get; private set; }

    public FragmentReceiver(IXBeeTransport transport, SessionManager sessionManager)
    {
        _transport = transport;
        _sessionManager = sessionManager;

        // Subscribe to session manager events
        _sessionManager.OnRxActivityTimeout += HandleActivityTimeout;
    }

    /// <summary>
    /// Process incoming RF data (fragment, NACK, or DONE)
    /// </summary>
    public void ProcessRfData(byte[] data, ulong sourceAddress)
    {
        if (data.Length < 2)
            return;

        var type = (FragmentType)data[1];

        switch (type)
        {
            case FragmentType.Data:
                ProcessDataFragment(data, sourceAddress);
                break;

            case FragmentType.Nack:
                // NACK is handled by transmitter, forward it
                var nack = NackMessage.FromBytes(data, 0, data.Length);
                if (nack != null)
                {
                    OnNackReceived?.Invoke(nack, sourceAddress);
                }
                break;

            case FragmentType.Done:
                var msgId = DoneMessage.FromBytes(data, 0, data.Length);
                if (msgId.HasValue)
                {
                    OnDoneReceived?.Invoke(msgId.Value);
                }
                break;
        }
    }

    /// <summary>
    /// Event for when NACK is received (for transmitter)
    /// </summary>
    public event Action<NackMessage, ulong>? OnNackReceived;

    /// <summary>
    /// Event for when DONE is received (for transmitter)
    /// </summary>
    public event Action<ushort>? OnDoneReceived;

    /// <summary>
    /// Process DATA fragment
    /// </summary>
    private void ProcessDataFragment(byte[] data, ulong sourceAddress)
    {
        // Minimum size: header + CRC
        if (data.Length < FragmentProtocol.HeaderSize + FragmentProtocol.CrcSize)
        {
            OnLog?.Invoke("Fragment too short");
            return;
        }

        // Verify CRC
        if (!Crc16.Verify(data, 0, data.Length))
        {
            CrcFailures++;
            OnLog?.Invoke("Fragment CRC failure");
            return;
        }

        TotalFragmentsReceived++;

        // Parse header
        var header = FragmentHeader.ReadFrom(data, 0);

        if (header.Version != FragmentProtocol.Version)
        {
            OnLog?.Invoke($"Unknown protocol version: {header.Version}");
            return;
        }

        // Get or create session
        var session = _sessionManager.GetOrCreateRxSession(
            header.MsgId, header.TotalLen, header.FragCnt, sourceAddress);

        // Store fragment payload
        if (header.FragIdx < session.FragCnt && !session.ReceivedBitmap[header.FragIdx])
        {
            var payload = new byte[header.PayloadLen];
            Array.Copy(data, FragmentProtocol.HeaderSize, payload, 0, header.PayloadLen);
            session.FragmentPayloads[header.FragIdx] = payload;
            session.ReceivedBitmap[header.FragIdx] = true;
            _sessionManager.UpdateRxActivity(header.MsgId);

            OnLog?.Invoke($"RX msg_id={header.MsgId}: fragment {header.FragIdx + 1}/{header.FragCnt}");
        }

        // Check if complete
        if (_sessionManager.IsRxComplete(header.MsgId))
        {
            CompleteMessage(header.MsgId, sourceAddress);
        }
        // Check if this is the last fragment and we have missing pieces
        else if (header.FragIdx == header.FragCnt - 1)
        {
            // Last fragment received but not complete - send NACK
            SendNack(header.MsgId, sourceAddress);
        }
    }

    /// <summary>
    /// Complete message reassembly and notify
    /// </summary>
    private void CompleteMessage(ushort msgId, ulong sourceAddress)
    {
        var data = _sessionManager.ReassembleMessage(msgId);
        if (data == null)
        {
            OnLog?.Invoke($"RX msg_id={msgId}: Reassembly failed");
            return;
        }

        MessagesCompleted++;
        OnLog?.Invoke($"RX msg_id={msgId}: Complete ({data.Length}B)");

        // Send DONE to sender
        var done = DoneMessage.ToBytes(msgId);
        _transport.SendDataNoWait(sourceAddress, done);

        // Cleanup session
        _sessionManager.RemoveRxSession(msgId);

        // Notify upper layer
        OnMessageReceived?.Invoke(data, sourceAddress);
    }

    /// <summary>
    /// Send NACK for missing fragments
    /// </summary>
    private void SendNack(ushort msgId, ulong destAddress)
    {
        var missing = _sessionManager.GetMissingFragments(msgId);
        if (missing.Length == 0)
            return;

        var session = _sessionManager.GetRxSession(msgId);
        if (session == null)
            return;

        session.NacksSent++;
        if (session.NacksSent > FragmentProtocol.MaxNackRounds)
        {
            OnLog?.Invoke($"RX msg_id={msgId}: Max NACK rounds exceeded, dropping");
            _sessionManager.RemoveRxSession(msgId);
            return;
        }

        var nack = new NackMessage
        {
            MsgId = msgId,
            MissingIndices = missing
        };

        _transport.SendDataNoWait(destAddress, nack.ToBytes());
        NacksSent++;

        OnLog?.Invoke($"RX msg_id={msgId}: NACK sent for {missing.Length} fragments");
        OnNackSent?.Invoke(msgId, missing);
    }

    /// <summary>
    /// Handle activity timeout - check if we need to send NACK
    /// </summary>
    private void HandleActivityTimeout(RxSession session)
    {
        if (!_sessionManager.IsRxComplete(session.MsgId))
        {
            SendNack(session.MsgId, session.SourceAddress);
            session.LastActivityTime = DateTime.UtcNow; // Reset to avoid immediate re-trigger
        }
    }

    /// <summary>
    /// Reset statistics
    /// </summary>
    public void ResetStats()
    {
        TotalFragmentsReceived = 0;
        CrcFailures = 0;
        NacksSent = 0;
        MessagesCompleted = 0;
    }

    /// <summary>
    /// Dispose and unsubscribe from events
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // CRITICAL: Unsubscribe from session manager event to prevent memory leak
        _sessionManager.OnRxActivityTimeout -= HandleActivityTimeout;

        GC.SuppressFinalize(this);
    }
}
