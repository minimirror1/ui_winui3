using System;
using System.Collections.Generic;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Interfaces
{
    public interface ISerialTrafficTap
    {
        int Capacity { get; }

        event EventHandler<SerialTrafficEntry>? EntryRecorded;

        IReadOnlyList<SerialTrafficEntry> GetSnapshot();

        void Clear();

        void RecordTx(string line);

        void RecordRx(string line);
    }
}

