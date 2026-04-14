using System;
using System.Collections.Generic;
using System.Linq;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Infrastructure
{
    public sealed class SerialTrafficTap : ISerialTrafficTap
    {
        private readonly object _lock = new();
        private readonly Queue<SerialTrafficEntry> _buffer;

        public int Capacity { get; }

        public event EventHandler<SerialTrafficEntry>? EntryRecorded;

        public SerialTrafficTap(int capacity = 10_000)
        {
            Capacity = capacity <= 0 ? 10_000 : capacity;
            _buffer = new Queue<SerialTrafficEntry>(Math.Min(Capacity, 1024));
        }

        public IReadOnlyList<SerialTrafficEntry> GetSnapshot()
        {
            lock (_lock)
            {
                return _buffer.ToArray();
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _buffer.Clear();
            }
        }

        public void RecordTx(string line) => Record(SerialTrafficDirection.Tx, line);

        public void RecordRx(string line) => Record(SerialTrafficDirection.Rx, line);

        public void RecordTxBytes(byte[] data) => Record(SerialTrafficDirection.Tx, ToHexDisplay(data));

        public void RecordRxBytes(byte[] data) => Record(SerialTrafficDirection.Rx, ToHexDisplay(data));

        private static string ToHexDisplay(byte[] data)
            => data == null ? string.Empty : string.Join(" ", data.Select(b => b.ToString("X2")));

        private void Record(SerialTrafficDirection direction, string line)
        {
            // Always be safe; monitor should never crash serial I/O.
            if (line == null) line = string.Empty;

            SerialTrafficEntry entry = new(DateTimeOffset.Now, direction, line);
            EventHandler<SerialTrafficEntry>? handler;

            lock (_lock)
            {
                _buffer.Enqueue(entry);
                while (_buffer.Count > Capacity)
                {
                    _buffer.Dequeue();
                }
                handler = EntryRecorded;
            }

            try
            {
                handler?.Invoke(this, entry);
            }
            catch
            {
                // Swallow to avoid breaking producer path.
            }
        }
    }
}






