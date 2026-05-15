using System;

namespace AnimatronicsControlCenter.Core.Models
{
    public enum SerialTrafficDirection
    {
        Tx = 0,
        Rx = 1
    }

    public readonly record struct SerialTrafficCounts(int TxCount, int RxCount)
    {
        public int TotalCount => TxCount + RxCount;
    }

    public sealed record SerialTrafficEntry(
        DateTimeOffset Timestamp,
        SerialTrafficDirection Direction,
        string Line)
    {
        public string Prefix => Direction == SerialTrafficDirection.Tx ? ">" : "<";

        public string TimestampText => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");

        public string DirectionArrow => Direction == SerialTrafficDirection.Tx ? "↑" : "↓";

        // Example: >[09:15:00.828]{"src_id":0,...}\n
        public string DisplayLine => $"{Prefix}[{TimestampText}]{Line}";
    }
}
