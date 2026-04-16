using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Protocol;
using AnimatronicsControlCenter.UI.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public enum SerialTrafficFilter
    {
        All = 0,
        Tx = 1,
        Rx = 2
    }

    public sealed class PacketItem
    {
        public required SerialTrafficEntry Traffic { get; init; }
        public string? Command { get; init; }
        public int? SrcId { get; init; }
        public int? TarId { get; init; }
        public string? Status { get; init; }
        // Binary 전환: RawJson → hex dump, PrettyJson → 디코딩 결과
        public string RawJson { get; init; } = string.Empty;    // hex dump ("00 02 01 ...")
        public string PrettyJson { get; init; } = string.Empty; // decoded header info
        public string? ParseError { get; init; }

        public string Summary
        {
            get
            {
                var dir    = Traffic.Prefix;
                var time   = Traffic.TimestampText;
                var cmd    = string.IsNullOrWhiteSpace(Command) ? "-" : Command;
                var src    = SrcId?.ToString() ?? "-";
                var tar    = TarId?.ToString() ?? "-";
                var status = string.IsNullOrWhiteSpace(Status) ? "-" : Status;
                return $"{dir}[{time}] cmd={cmd} src={src} tar={tar} status={status}";
            }
        }
    }

    public partial class SerialMonitorViewModel : ObservableObject
    {
        private const int DefaultMaxLines = 10_000;

        private readonly ISerialTrafficTap _tap;
        private readonly IComRawTrafficTap _comRawTap;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly DispatcherQueueTimer _flushTimer;

        private readonly object _pendingLock = new();
        private readonly Queue<SerialTrafficEntry> _pending = new();
        private readonly Queue<SerialTrafficEntry> _pendingComRaw = new();

        private readonly List<SerialTrafficEntry> _allEntries = new(DefaultMaxLines);
        private readonly List<PacketItem> _allPackets = new(DefaultMaxLines);
        private readonly List<SerialTrafficEntry> _allComRawEntries = new(DefaultMaxLines);

        public LocalizedStrings Strings { get; }

        public ObservableCollection<SerialTrafficEntry> Entries { get; } = new();
        public ObservableCollection<SerialTrafficEntry> ComRawEntries { get; } = new();
        public ObservableCollection<PacketItem> Packets { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PauseButtonText))]
        private bool isPaused;

        [ObservableProperty]
        private bool isAutoScrollEnabled = true;

        [ObservableProperty]
        private SerialTrafficFilter filter = SerialTrafficFilter.All;

        [ObservableProperty]
        private int filterIndex;

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private SerialTrafficEntry? selectedEntry;

        [ObservableProperty]
        private PacketItem? selectedPacket;

        [ObservableProperty]
        private int parseErrorCount;

        [ObservableProperty]
        private bool isComRawCaptureEnabled;

        [ObservableProperty]
        private int selectedTabIndex;

        public string PauseButtonText => IsPaused
            ? Strings.Get("SerialMonitor_Resume", Strings.Code)
            : Strings.Get("SerialMonitor_Pause", Strings.Code);

        public SerialMonitorViewModel(
            ISerialTrafficTap tap,
            IComRawTrafficTap comRawTap,
            ILocalizationService localizationService)
        {
            _tap = tap;
            _comRawTap = comRawTap;
            Strings = new LocalizedStrings(localizationService);

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            foreach (var entry in _tap.GetSnapshot())
            {
                AppendToAll(entry);
            }

            foreach (var entry in _comRawTap.GetSnapshot())
            {
                AppendToComRawAll(entry);
            }

            RebuildVisible();

            _tap.EntryRecorded += TapOnEntryRecorded;
            _comRawTap.EntryRecorded += TapOnComRawEntryRecorded;

            _flushTimer = _dispatcherQueue.CreateTimer();
            _flushTimer.Interval = TimeSpan.FromMilliseconds(75);
            _flushTimer.Tick += (_, _) => FlushPendingToUi();
            _flushTimer.Start();

            FilterIndex = (int)Filter;
            IsComRawCaptureEnabled = _comRawTap.IsCaptureEnabled;
        }

        private void TapOnEntryRecorded(object? sender, SerialTrafficEntry entry)
        {
            lock (_pendingLock)
            {
                _pending.Enqueue(entry);
                while (_pending.Count > 2_000)
                {
                    _pending.Dequeue();
                }
            }
        }

        private void TapOnComRawEntryRecorded(object? sender, SerialTrafficEntry entry)
        {
            lock (_pendingLock)
            {
                _pendingComRaw.Enqueue(entry);
                while (_pendingComRaw.Count > 2_000)
                {
                    _pendingComRaw.Dequeue();
                }
            }
        }

        partial void OnIsPausedChanged(bool value)
        {
            if (!value)
            {
                FlushPendingToUi(force: true);
                RebuildVisible();
            }
        }

        partial void OnFilterChanged(SerialTrafficFilter value)
        {
            if (FilterIndex != (int)value)
            {
                FilterIndex = (int)value;
            }
            RebuildVisible();
        }

        partial void OnFilterIndexChanged(int value)
        {
            // WinUI ComboBox can transiently report -1 (no selection) during template/binding updates.
            // Treat it as "ignore" to avoid clearing/rebuilding lists (flicker) on every new item.
            if (value < 0) return;
            if (value > 2) return;

            var next = (SerialTrafficFilter)value;
            if (Filter == next) return;
            Filter = next;
        }

        partial void OnIsComRawCaptureEnabledChanged(bool value)
        {
            _comRawTap.IsCaptureEnabled = value;
        }

        private void FlushPendingToUi(bool force = false)
        {
            if (IsPaused && !force) return;

            List<SerialTrafficEntry> drained = new();
            List<SerialTrafficEntry> drainedComRaw = new();
            lock (_pendingLock)
            {
                while (_pending.Count > 0 && drained.Count < 500)
                {
                    drained.Add(_pending.Dequeue());
                }

                while (_pendingComRaw.Count > 0 && drainedComRaw.Count < 500)
                {
                    drainedComRaw.Add(_pendingComRaw.Dequeue());
                }
            }

            foreach (var entry in drained)
            {
                AppendToAll(entry);
                if (MatchesFilter(entry))
                {
                    Entries.Add(entry);
                }
            }

            foreach (var entry in drainedComRaw)
            {
                AppendToComRawAll(entry);
                if (MatchesFilter(entry))
                {
                    ComRawEntries.Add(entry);
                }
            }

            if (drained.Count == 0 && drainedComRaw.Count == 0) return;
            TrimIfNeeded();
        }

        private void AppendToAll(SerialTrafficEntry entry)
        {
            _allEntries.Add(entry);

            var packet = TryBuildPacket(entry);
            if (packet != null)
            {
                _allPackets.Add(packet);
                if (packet.ParseError != null) ParseErrorCount++;
                if (MatchesFilter(entry))
                {
                    Packets.Add(packet);
                }
            }
        }

        private void AppendToComRawAll(SerialTrafficEntry entry)
        {
            _allComRawEntries.Add(entry);
        }

        private PacketItem? TryBuildPacket(SerialTrafficEntry entry)
        {
            var hexLine = entry.Line?.Trim();
            if (string.IsNullOrWhiteSpace(hexLine)) return null;

            // hex 문자열("00 02 01 00 00") → byte[]
            byte[]? data = TryParseHex(hexLine);
            if (data == null || data.Length == 0)
            {
                return new PacketItem
                {
                    Traffic    = entry,
                    RawJson    = hexLine,
                    PrettyJson = hexLine,
                    ParseError = "Not a binary hex packet"
                };
            }

            // 응답 헤더 시도 (6 bytes 이상)
            if (BinaryDeserializer.TryParseResponseHeader(data, out var respHdr))
            {
                string decoded = $"RESP cmd={respHdr.Cmd}(0x{(byte)respHdr.Cmd:X2}) " +
                                 $"status={respHdr.Status} src={respHdr.SrcId} tar={respHdr.TarId} " +
                                 $"payloadLen={respHdr.PayloadLen}";
                return new PacketItem
                {
                    Traffic    = entry,
                    RawJson    = hexLine,
                    PrettyJson = decoded,
                    Command    = respHdr.Cmd.ToString(),
                    SrcId      = respHdr.SrcId,
                    TarId      = respHdr.TarId,
                    Status     = respHdr.Status.ToString(),
                };
            }

            // 요청 헤더 시도 (5 bytes 이상)
            if (BinaryDeserializer.TryParseRequestHeader(data, out var reqHdr))
            {
                string decoded = $"REQ  cmd={reqHdr.Cmd}(0x{(byte)reqHdr.Cmd:X2}) " +
                                 $"src={reqHdr.SrcId} tar={reqHdr.TarId} payloadLen={reqHdr.PayloadLen}";
                return new PacketItem
                {
                    Traffic    = entry,
                    RawJson    = hexLine,
                    PrettyJson = decoded,
                    Command    = reqHdr.Cmd.ToString(),
                    SrcId      = reqHdr.SrcId,
                    TarId      = reqHdr.TarId,
                    Status     = null,
                };
            }

            return new PacketItem
            {
                Traffic    = entry,
                RawJson    = hexLine,
                PrettyJson = hexLine,
                ParseError = "Cannot parse binary header"
            };
        }

        private static byte[]? TryParseHex(string hex)
        {
            try
            {
                var tokens = hex.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var bytes  = new byte[tokens.Length];
                for (int i = 0; i < tokens.Length; i++)
                    bytes[i] = Convert.ToByte(tokens[i], 16);
                return bytes;
            }
            catch
            {
                return null;
            }
        }

        private bool MatchesFilter(SerialTrafficEntry entry) =>
            Filter switch
            {
                SerialTrafficFilter.Tx => entry.Direction == SerialTrafficDirection.Tx,
                SerialTrafficFilter.Rx => entry.Direction == SerialTrafficDirection.Rx,
                _ => true
            };

        private void RebuildVisible()
        {
            Entries.Clear();
            ComRawEntries.Clear();
            Packets.Clear();

            foreach (var entry in _allEntries.Where(MatchesFilter))
            {
                Entries.Add(entry);
            }

            foreach (var entry in _allComRawEntries.Where(MatchesFilter))
            {
                ComRawEntries.Add(entry);
            }

            foreach (var packet in _allPackets.Where(p => MatchesFilter(p.Traffic)))
            {
                Packets.Add(packet);
            }
        }

        private void TrimIfNeeded()
        {
            // IMPORTANT:
            // When we are at capacity, trimming must NOT clear/rebuild visible collections,
            // otherwise the packet list will flicker on every incoming packet.
            while (_allEntries.Count > DefaultMaxLines)
            {
                var removedEntry = _allEntries[0];
                _allEntries.RemoveAt(0);

                // Remove from visible entries if present (depends on current filter).
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Equals(Entries[i], removedEntry))
                    {
                        Entries.RemoveAt(i);
                        break;
                    }
                }

                // Remove associated packet (if any) from all/visible packet lists.
                int allPacketIndex = _allPackets.FindIndex(p => Equals(p.Traffic, removedEntry));
                if (allPacketIndex >= 0)
                {
                    var removedPacket = _allPackets[allPacketIndex];
                    _allPackets.RemoveAt(allPacketIndex);

                    if (removedPacket.ParseError != null && ParseErrorCount > 0)
                    {
                        ParseErrorCount--;
                    }

                    for (int i = 0; i < Packets.Count; i++)
                    {
                        if (Equals(Packets[i].Traffic, removedEntry))
                        {
                            Packets.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            while (_allComRawEntries.Count > DefaultMaxLines)
            {
                var removedEntry = _allComRawEntries[0];
                _allComRawEntries.RemoveAt(0);

                for (int i = 0; i < ComRawEntries.Count; i++)
                {
                    if (Equals(ComRawEntries[i], removedEntry))
                    {
                        ComRawEntries.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        [RelayCommand]
        private void TogglePause()
        {
            IsPaused = !IsPaused;
        }

        [RelayCommand]
        private void Clear()
        {
            lock (_pendingLock)
            {
                _pending.Clear();
                _pendingComRaw.Clear();
            }
            _tap.Clear();
            _comRawTap.Clear();
            _allEntries.Clear();
            _allPackets.Clear();
            _allComRawEntries.Clear();
            Entries.Clear();
            ComRawEntries.Clear();
            Packets.Clear();
            ParseErrorCount = 0;
            SelectedEntry = null;
            SelectedPacket = null;
        }

        [RelayCommand]
        private void CopyAll()
        {
            var source = SelectedTabIndex == 2 ? ComRawEntries : Entries;
            var text = string.Join(Environment.NewLine, source.Select(e => e.DisplayLine));
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }

        [RelayCommand]
        private void CopySelected(object? selectedItems)
        {
            // WinUI ListView.SelectedItems is not guaranteed to be System.Collections.IList.
            // It is often a projected generic list type (e.g. IList<object>), which would
            // fail CommunityToolkit's strict RelayCommand<T> argument validation.
            if (selectedItems is not IEnumerable enumerable) return;

            var lines = new List<string>();
            foreach (var item in enumerable)
            {
                if (item is SerialTrafficEntry entry)
                {
                    lines.Add(entry.DisplayLine);
                }
            }

            if (lines.Count == 0) return;

            var text = string.Join(Environment.NewLine, lines);
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }

        [RelayCommand]
        private void FindNext()
        {
            if (string.IsNullOrWhiteSpace(SearchText) || Entries.Count == 0) return;

            var startIndex = SelectedEntry != null ? Entries.IndexOf(SelectedEntry) : -1;
            for (int i = 1; i <= Entries.Count; i++)
            {
                var idx = (startIndex + i) % Entries.Count;
                if (Entries[idx].DisplayLine.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedEntry = Entries[idx];
                    return;
                }
            }
        }

        [RelayCommand]
        private void FindPrevious()
        {
            if (string.IsNullOrWhiteSpace(SearchText) || Entries.Count == 0) return;

            var startIndex = SelectedEntry != null ? Entries.IndexOf(SelectedEntry) : 0;
            for (int i = 1; i <= Entries.Count; i++)
            {
                var idx = (startIndex - i) % Entries.Count;
                if (idx < 0) idx += Entries.Count;
                if (Entries[idx].DisplayLine.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedEntry = Entries[idx];
                    return;
                }
            }
        }

        public async System.Threading.Tasks.Task SaveToFileAsync(nint windowHandle)
        {
            var picker = new FileSavePicker();
            InitializeWithWindow.Initialize(picker, windowHandle);

            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.SuggestedFileName = SelectedTabIndex == 2 ? "com-raw-monitor" : "serial-monitor";
            picker.FileTypeChoices.Add("Log", new List<string> { ".log", ".txt" });

            StorageFile? file = await picker.PickSaveFileAsync();
            if (file == null) return;

            var source = SelectedTabIndex == 2 ? ComRawEntries : Entries;
            var content = string.Join(Environment.NewLine, source.Select(e => e.DisplayLine)) + Environment.NewLine;
            await FileIO.WriteTextAsync(file, content);
        }
    }
}






