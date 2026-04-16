using System;
using System.Collections.Generic;
using System.Text;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Infrastructure
{
    public sealed class ComRawTrafficTap : IComRawTrafficTap
    {
        private readonly object _lock = new();
        private readonly Queue<SerialTrafficEntry> _buffer;

        public int Capacity { get; }

        public bool IsCaptureEnabled { get; set; }

        public event EventHandler<SerialTrafficEntry>? EntryRecorded;

        public ComRawTrafficTap(int capacity = 10_000)
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

        public void RecordTxBytes(byte[] data) => RecordTxBytes(data, 0, data?.Length ?? 0);

        public void RecordRxBytes(byte[] data) => RecordRxBytes(data, 0, data?.Length ?? 0);

        public void RecordTxBytes(byte[] data, int offset, int count)
            => Record(SerialTrafficDirection.Tx, data, offset, count);

        public void RecordRxBytes(byte[] data, int offset, int count)
            => Record(SerialTrafficDirection.Rx, data, offset, count);

        private void Record(SerialTrafficDirection direction, byte[]? data, int offset, int count)
        {
            if (!IsCaptureEnabled) return;
            if (data == null || count <= 0) return;
            if (offset < 0 || count < 0 || offset + count > data.Length) return;

            SerialTrafficEntry entry = new(DateTimeOffset.Now, direction, ToHexDisplay(data, offset, count));
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
                // Swallow to avoid breaking the serial I/O path.
            }
        }

        private static string ToHexDisplay(byte[] data, int offset, int count)
        {
            var builder = new StringBuilder(count * 3 - 1);
            for (int i = 0; i < count; i++)
            {
                if (i > 0) builder.Append(' ');
                builder.Append(data[offset + i].ToString("X2"));
            }
            return builder.ToString();
        }
    }
}
