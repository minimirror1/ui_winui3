using System;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;

namespace AnimatronicsControlCenter.Core.Link;

/// <summary>
/// Handles message fragmentation and transmission with NACK-based retransmission
/// </summary>
public class FragmentTransmitter
{
    private readonly IXBeeTransport _transport;
    private readonly SessionManager _sessionManager;

    /// <summary>
    /// Event for logging
    /// </summary>
    public event Action<string>? OnLog;

    /// <summary>
    /// Statistics
    /// </summary>
    public int TotalFragmentsSent { get; private set; }
    public int RetransmittedFragments { get; private set; }
    public int DroppedFragments { get; private set; }

    /// <summary>
    /// Payload size per fragment (configurable MTU)
    /// Default is FragmentProtocol.MaxPayloadSize (30B)
    /// Max is 34B for DigiMesh with encryption (NP=49 - Header=13 - CRC=2)
    /// </summary>
    private int _payloadSize = FragmentProtocol.MaxPayloadSize;
    public int PayloadSize
    {
        get => _payloadSize;
        set => _payloadSize = Math.Clamp(value, 10, 34);
    }

    public FragmentTransmitter(IXBeeTransport transport, SessionManager sessionManager)
    {
        _transport = transport;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// Send a message with fragmentation
    /// </summary>
    public async Task<bool> SendMessageAsync(byte[] data, ulong destAddress64,
        CancellationToken ct = default)
    {
        if (data.Length > FragmentProtocol.MaxMessageSize)
        {
            OnLog?.Invoke($"Message too large: {data.Length} bytes (max {FragmentProtocol.MaxMessageSize})");
            return false;
        }

        // Create fragments
        var fragments = CreateFragments(data, out var msgId);
        var session = _sessionManager.CreateTxSession(msgId, data, fragments);

        OnLog?.Invoke($"TX msg_id={msgId}: {data.Length}B → {fragments.Length} fragments");

        try
        {
            // Calculate delay based on fragment count to avoid buffer overflow
            // More fragments = more delay needed for reliable transmission
            int delayMs = fragments.Length switch
            {
                <= 10 => 10,   // Small messages: 10ms
                <= 30 => 15,   // Medium messages: 15ms
                <= 50 => 20,   // Large messages: 20ms
                _ => 30        // Very large messages: 30ms
            };

            // Send all fragments initially
            for (int i = 0; i < fragments.Length; i++)
            {
                if (ct.IsCancellationRequested)
                    return false;

                _transport.SendDataNoWait(destAddress64, fragments[i]);
                TotalFragmentsSent++;

                // Delay between fragments to avoid buffer overflow
                if (i < fragments.Length - 1)
                    await Task.Delay(delayMs, ct);
            }

            // Wait for completion (DONE message) or timeout
            using var timeoutCts = new CancellationTokenSource(FragmentProtocol.SessionTimeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

            try
            {
                return await session.Completion!.Task.WaitAsync(linked.Token);
            }
            catch (OperationCanceledException)
            {
                OnLog?.Invoke($"TX msg_id={msgId}: Timeout");
                return false;
            }
        }
        finally
        {
            _sessionManager.CompleteTxSession(msgId, false);
        }
    }

    /// <summary>
    /// Handle incoming NACK - retransmit missing fragments
    /// </summary>
    public void HandleNack(NackMessage nack, ulong destAddress64)
    {
        var session = _sessionManager.GetTxSession(nack.MsgId);
        if (session == null)
        {
            OnLog?.Invoke($"NACK for unknown msg_id={nack.MsgId}");
            return;
        }

        session.NackRounds++;
        if (session.NackRounds > FragmentProtocol.MaxNackRounds)
        {
            OnLog?.Invoke($"TX msg_id={nack.MsgId}: Max NACK rounds exceeded");
            _sessionManager.CompleteTxSession(nack.MsgId, false);
            return;
        }

        OnLog?.Invoke($"TX msg_id={nack.MsgId}: NACK round {session.NackRounds}, " +
                      $"retransmitting {nack.MissingIndices.Length} fragments");

        // Retransmit missing fragments with delay to avoid buffer overflow
        int retransmitCount = 0;
        foreach (var idx in nack.MissingIndices)
        {
            if (idx < session.Fragments.Length)
            {
                _transport.SendDataNoWait(destAddress64, session.Fragments[idx]);
                TotalFragmentsSent++;
                RetransmittedFragments++;
                retransmitCount++;

                // Add delay every few fragments to prevent buffer overflow
                if (retransmitCount % 5 == 0)
                {
                    Thread.Sleep(20); // Brief pause every 5 fragments
                }
            }
        }
    }

    /// <summary>
    /// Handle incoming DONE - mark session complete
    /// </summary>
    public void HandleDone(ushort msgId)
    {
        OnLog?.Invoke($"TX msg_id={msgId}: DONE received");
        _sessionManager.CompleteTxSession(msgId, true);
    }

    /// <summary>
    /// Create fragment payloads from message data
    /// </summary>
    private byte[][] CreateFragments(byte[] data, out ushort msgId)
    {
        msgId = _sessionManager.GetNextMsgId();

        // Use configurable PayloadSize (MTU)
        int payloadSize = PayloadSize;
        int fragCount = (data.Length + payloadSize - 1) / payloadSize;
        if (fragCount == 0) fragCount = 1;

        var fragments = new byte[fragCount][];
        int offset = 0;

        for (int i = 0; i < fragCount; i++)
        {
            int payloadLen = Math.Min(payloadSize, data.Length - offset);
            int totalSize = FragmentProtocol.HeaderSize + payloadLen + FragmentProtocol.CrcSize;

            var fragment = new byte[totalSize];

            // Write header
            var header = new FragmentHeader
            {
                Version = FragmentProtocol.Version,
                Type = FragmentType.Data,
                MsgId = msgId,
                TotalLen = (uint)data.Length,
                FragIdx = (ushort)i,
                FragCnt = (ushort)fragCount,
                PayloadLen = (byte)payloadLen
            };
            header.WriteTo(fragment, 0);

            // Write payload
            Array.Copy(data, offset, fragment, FragmentProtocol.HeaderSize, payloadLen);
            offset += payloadLen;

            // Write CRC (over header + payload)
            Crc16.Append(fragment, 0, totalSize - FragmentProtocol.CrcSize);

            fragments[i] = fragment;
        }

        return fragments;
    }

    /// <summary>
    /// Reset statistics
    /// </summary>
    public void ResetStats()
    {
        TotalFragmentsSent = 0;
        RetransmittedFragments = 0;
        DroppedFragments = 0;
    }
}
