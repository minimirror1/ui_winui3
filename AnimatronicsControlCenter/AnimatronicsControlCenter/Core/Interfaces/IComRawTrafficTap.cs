using System;
using System.Collections.Generic;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Interfaces
{
    public interface IComRawTrafficTap
    {
        int Capacity { get; }

        bool IsCaptureEnabled { get; set; }

        event EventHandler<SerialTrafficEntry>? EntryRecorded;

        IReadOnlyList<SerialTrafficEntry> GetSnapshot();

        void Clear();

        void RecordTxBytes(byte[] data);

        void RecordRxBytes(byte[] data);

        void RecordTxBytes(byte[] data, int offset, int count);

        void RecordRxBytes(byte[] data, int offset, int count);
    }
}
