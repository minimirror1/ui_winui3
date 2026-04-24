using System;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Utilities;

public readonly record struct SerialTrafficIndicatorSnapshot(bool IsRxActive, bool IsTxActive);

public sealed class SerialTrafficIndicatorState
{
    public static readonly TimeSpan ActivityWindow = TimeSpan.FromMilliseconds(350);

    public DateTimeOffset? LastRxAt { get; private set; }

    public DateTimeOffset? LastTxAt { get; private set; }

    public void Record(SerialTrafficEntry entry)
    {
        if (entry.Direction == SerialTrafficDirection.Rx)
        {
            LastRxAt = entry.Timestamp;
            return;
        }

        LastTxAt = entry.Timestamp;
    }

    public SerialTrafficIndicatorSnapshot GetSnapshot(DateTimeOffset now)
        => new(
            IsRxActive: IsWithinActivityWindow(LastRxAt, now),
            IsTxActive: IsWithinActivityWindow(LastTxAt, now));

    private static bool IsWithinActivityWindow(DateTimeOffset? lastActivityAt, DateTimeOffset now)
        => lastActivityAt.HasValue && now - lastActivityAt.Value <= ActivityWindow;
}
