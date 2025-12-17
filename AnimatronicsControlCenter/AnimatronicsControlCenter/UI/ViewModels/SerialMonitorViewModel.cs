using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.UI.Helpers;
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
        public string RawJson { get; init; } = string.Empty;
        public string PrettyJson { get; init; } = string.Empty;
        public string? ParseError { get; init; }

        public string Summary
        {
            get
            {
                var dir = Traffic.Prefix;
                var time = Traffic.TimestampText;
                var cmd = string.IsNullOrWhiteSpace(Command) ? "-" : Command;
                var src = SrcId?.ToString() ?? "-";
                var tar = TarId?.ToString() ?? "-";
                var status = string.IsNullOrWhiteSpace(Status) ? "-" : Status;
                return $"{dir}[{time}] cmd={cmd} src={src} tar={tar} status={status}";
            }
        }
    }

    public partial class SerialMonitorViewModel : ObservableObject
    {
        private const int DefaultMaxLines = 10_000;

        private readonly ISerialTrafficTap _tap;
        private readonly DispatcherQueue _dispatcherQueue;
        private readonly DispatcherQueueTimer _flushTimer;

        private readonly object _pendingLock = new();
        private readonly Queue<SerialTrafficEntry> _pending = new();

        private readonly List<SerialTrafficEntry> _allEntries = new(DefaultMaxLines);
        private readonly List<PacketItem> _allPackets = new(DefaultMaxLines);

        public LocalizedStrings Strings { get; }

        public ObservableCollection<SerialTrafficEntry> Entries { get; } = new();
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

        public string PauseButtonText => IsPaused
            ? Strings.Get("SerialMonitor_Resume", Strings.Code)
            : Strings.Get("SerialMonitor_Pause", Strings.Code);

        public SerialMonitorViewModel(ISerialTrafficTap tap, ILocalizationService localizationService)
        {
            _tap = tap;
            Strings = new LocalizedStrings(localizationService);

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            foreach (var entry in _tap.GetSnapshot())
            {
                AppendToAll(entry);
            }
            RebuildVisible();

            _tap.EntryRecorded += TapOnEntryRecorded;

            _flushTimer = _dispatcherQueue.CreateTimer();
            _flushTimer.Interval = TimeSpan.FromMilliseconds(75);
            _flushTimer.Tick += (_, _) => FlushPendingToUi();
            _flushTimer.Start();

            FilterIndex = (int)Filter;
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

        private void FlushPendingToUi(bool force = false)
        {
            if (IsPaused && !force) return;

            List<SerialTrafficEntry> drained = new();
            lock (_pendingLock)
            {
                while (_pending.Count > 0 && drained.Count < 500)
                {
                    drained.Add(_pending.Dequeue());
                }
            }

            if (drained.Count == 0) return;

            foreach (var entry in drained)
            {
                AppendToAll(entry);
                if (MatchesFilter(entry))
                {
                    Entries.Add(entry);
                }
            }

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

        private static string StripFramingMarker(string line)
        {
            if (string.IsNullOrEmpty(line)) return string.Empty;
            return line.EndsWith("\\n", StringComparison.Ordinal) ? line[..^2] : line;
        }

        private PacketItem? TryBuildPacket(SerialTrafficEntry entry)
        {
            var raw = StripFramingMarker(entry.Line);
            if (string.IsNullOrWhiteSpace(raw)) return null;

            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

                string? cmd = doc.RootElement.TryGetProperty("cmd", out var cmdEl) ? cmdEl.GetString() : null;
                int? src = doc.RootElement.TryGetProperty("src_id", out var srcEl) && srcEl.ValueKind == JsonValueKind.Number ? srcEl.GetInt32() : null;
                int? tar = doc.RootElement.TryGetProperty("tar_id", out var tarEl) && tarEl.ValueKind == JsonValueKind.Number ? tarEl.GetInt32() : null;
                string? status = doc.RootElement.TryGetProperty("status", out var stEl) ? stEl.GetString() : null;

                var pretty = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
                return new PacketItem
                {
                    Traffic = entry,
                    RawJson = raw,
                    PrettyJson = pretty,
                    Command = cmd,
                    SrcId = src,
                    TarId = tar,
                    Status = status
                };
            }
            catch (Exception ex)
            {
                // Still show the packet in packet monitor with parse error.
                return new PacketItem
                {
                    Traffic = entry,
                    RawJson = raw,
                    PrettyJson = raw,
                    ParseError = ex.Message
                };
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
            Packets.Clear();

            foreach (var entry in _allEntries.Where(MatchesFilter))
            {
                Entries.Add(entry);
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
            }
            _tap.Clear();
            _allEntries.Clear();
            _allPackets.Clear();
            Entries.Clear();
            Packets.Clear();
            ParseErrorCount = 0;
            SelectedEntry = null;
            SelectedPacket = null;
        }

        [RelayCommand]
        private void CopyAll()
        {
            var text = string.Join(Environment.NewLine, Entries.Select(e => e.DisplayLine));
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
            picker.SuggestedFileName = "serial-monitor";
            picker.FileTypeChoices.Add("Log", new List<string> { ".log", ".txt" });

            StorageFile? file = await picker.PickSaveFileAsync();
            if (file == null) return;

            var content = string.Join(Environment.NewLine, Entries.Select(e => e.DisplayLine)) + Environment.NewLine;
            await FileIO.WriteTextAsync(file, content);
        }
    }
}

