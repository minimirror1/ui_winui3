using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AnimatronicsControlCenter.Core.Link;

/// <summary>
/// Transmission session (sender side)
/// </summary>
public class TxSession
{
    public ushort MsgId { get; set; }
    public byte[] OriginalData { get; set; } = [];
    public byte[][] Fragments { get; set; } = [];
    public DateTime StartTime { get; set; }
    public int NackRounds { get; set; }
    public TaskCompletionSource<bool>? Completion { get; set; }
}

/// <summary>
/// Reception session (receiver side)
/// </summary>
public class RxSession
{
    public ushort MsgId { get; set; }
    public uint TotalLen { get; set; }
    public ushort FragCnt { get; set; }
    public byte[]?[] FragmentPayloads { get; set; } = [];
    public bool[] ReceivedBitmap { get; set; } = [];
    public DateTime LastActivityTime { get; set; }
    public DateTime StartTime { get; set; }
    public int NacksSent { get; set; }
    public ulong SourceAddress { get; set; }
}

/// <summary>
/// Manages TX and RX sessions
/// </summary>
public class SessionManager : IDisposable
{
    private readonly ConcurrentDictionary<ushort, TxSession> _txSessions = new();
    private readonly ConcurrentDictionary<ushort, RxSession> _rxSessions = new();
    private ushort _nextMsgId = 1;
    private readonly object _msgIdLock = new();
    private Timer? _cleanupTimer;

    /// <summary>
    /// Event raised when an RX session times out
    /// </summary>
    public event Action<ushort>? OnRxSessionTimeout;

    /// <summary>
    /// Event raised when activity timeout occurs (need to check for NACK)
    /// </summary>
    public event Action<RxSession>? OnRxActivityTimeout;

    public SessionManager()
    {
        // Cleanup timer every 500ms
        _cleanupTimer = new Timer(CleanupExpiredSessions, null, 500, 500);
    }

    /// <summary>
    /// Get next message ID
    /// </summary>
    public ushort GetNextMsgId()
    {
        lock (_msgIdLock)
        {
            ushort id = _nextMsgId++;
            if (_nextMsgId == 0) _nextMsgId = 1;
            return id;
        }
    }

    #region TX Sessions

    public TxSession CreateTxSession(ushort msgId, byte[] data, byte[][] fragments)
    {
        var session = new TxSession
        {
            MsgId = msgId,
            OriginalData = data,
            Fragments = fragments,
            StartTime = DateTime.UtcNow,
            Completion = new TaskCompletionSource<bool>()
        };

        _txSessions[msgId] = session;
        return session;
    }

    public TxSession? GetTxSession(ushort msgId)
    {
        _txSessions.TryGetValue(msgId, out var session);
        return session;
    }

    public void CompleteTxSession(ushort msgId, bool success)
    {
        if (_txSessions.TryRemove(msgId, out var session))
        {
            session.Completion?.TrySetResult(success);

            // Explicitly clear large data to help GC
            session.OriginalData = [];
            session.Fragments = [];
        }
    }

    #endregion

    #region RX Sessions

    public RxSession GetOrCreateRxSession(ushort msgId, uint totalLen, ushort fragCnt, ulong sourceAddr)
    {
        return _rxSessions.GetOrAdd(msgId, _ => new RxSession
        {
            MsgId = msgId,
            TotalLen = totalLen,
            FragCnt = fragCnt,
            FragmentPayloads = new byte[fragCnt][],
            ReceivedBitmap = new bool[fragCnt],
            StartTime = DateTime.UtcNow,
            LastActivityTime = DateTime.UtcNow,
            SourceAddress = sourceAddr
        });
    }

    public RxSession? GetRxSession(ushort msgId)
    {
        _rxSessions.TryGetValue(msgId, out var session);
        return session;
    }

    public void RemoveRxSession(ushort msgId)
    {
        if (_rxSessions.TryRemove(msgId, out var session))
        {
            // Explicitly clear large data to help GC
            session.FragmentPayloads = [];
            session.ReceivedBitmap = [];
        }
    }

    public void UpdateRxActivity(ushort msgId)
    {
        if (_rxSessions.TryGetValue(msgId, out var session))
        {
            session.LastActivityTime = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Get missing fragment indices for a session
    /// </summary>
    public ushort[] GetMissingFragments(ushort msgId)
    {
        if (!_rxSessions.TryGetValue(msgId, out var session))
            return [];

        var missing = new List<ushort>();
        for (int i = 0; i < session.ReceivedBitmap.Length; i++)
        {
            if (!session.ReceivedBitmap[i])
                missing.Add((ushort)i);
        }
        return missing.ToArray();
    }

    /// <summary>
    /// Check if all fragments are received
    /// </summary>
    public bool IsRxComplete(ushort msgId)
    {
        if (!_rxSessions.TryGetValue(msgId, out var session))
            return false;

        foreach (var received in session.ReceivedBitmap)
        {
            if (!received) return false;
        }
        return true;
    }

    /// <summary>
    /// Reassemble complete message
    /// </summary>
    public byte[]? ReassembleMessage(ushort msgId)
    {
        if (!_rxSessions.TryGetValue(msgId, out var session))
            return null;

        if (!IsRxComplete(msgId))
            return null;

        var result = new byte[session.TotalLen];
        int offset = 0;

        for (int i = 0; i < session.FragCnt; i++)
        {
            var payload = session.FragmentPayloads[i];
            if (payload == null) return null;

            Array.Copy(payload, 0, result, offset, payload.Length);
            offset += payload.Length;
        }

        return result;
    }

    #endregion

    /// <summary>
    /// Cleanup expired sessions
    /// </summary>
    private void CleanupExpiredSessions(object? state)
    {
        try
        {
            var now = DateTime.UtcNow;

            // Take snapshot of keys to avoid collection modification during iteration
            var rxKeys = _rxSessions.Keys.ToArray();
            var txKeys = _txSessions.Keys.ToArray();

            // Check RX sessions
            foreach (var key in rxKeys)
            {
                if (!_rxSessions.TryGetValue(key, out var session))
                    continue;

                var sessionAge = (now - session.StartTime).TotalMilliseconds;
                var inactiveTime = (now - session.LastActivityTime).TotalMilliseconds;

                // Session expired
                if (sessionAge > FragmentProtocol.SessionTimeoutMs)
                {
                    if (_rxSessions.TryRemove(key, out _))
                    {
                        try { OnRxSessionTimeout?.Invoke(key); } catch { }
                    }
                }
                // Activity timeout - may need NACK
                else if (inactiveTime > FragmentProtocol.FragmentTimeoutMs &&
                         !IsRxComplete(key))
                {
                    try { OnRxActivityTimeout?.Invoke(session); } catch { }
                }
            }

            // Check TX sessions
            foreach (var key in txKeys)
            {
                if (!_txSessions.TryGetValue(key, out var session))
                    continue;

                var sessionAge = (now - session.StartTime).TotalMilliseconds;

                if (sessionAge > FragmentProtocol.SessionTimeoutMs)
                {
                    CompleteTxSession(key, false);
                }
            }
        }
        catch (Exception)
        {
            // Swallow exceptions in timer callback to prevent crashes
        }
    }

    /// <summary>
    /// Clear all sessions (for cleanup between tests)
    /// </summary>
    public void ClearAllSessions()
    {
        // Clear TX sessions
        foreach (var key in _txSessions.Keys.ToArray())
        {
            CompleteTxSession(key, false);
        }

        // Clear RX sessions
        foreach (var key in _rxSessions.Keys.ToArray())
        {
            RemoveRxSession(key);
        }
    }

    /// <summary>
    /// Get current session counts for debugging
    /// </summary>
    public (int TxCount, int RxCount) GetSessionCounts()
    {
        return (_txSessions.Count, _rxSessions.Count);
    }

    public void Dispose()
    {
        _cleanupTimer?.Dispose();
        _cleanupTimer = null;

        // Clear all sessions on dispose
        ClearAllSessions();

        GC.SuppressFinalize(this);
    }
}
