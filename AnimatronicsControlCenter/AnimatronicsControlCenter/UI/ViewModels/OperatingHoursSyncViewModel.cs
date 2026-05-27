using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnimatronicsControlCenter.UI.ViewModels;

// ─── Day metadata ──────────────────────────────────────────────────────────────

file static class OpsDayMeta
{
    public static readonly (string Key, string Label, bool IsWeekend, bool IsSunday)[] All =
    [
        ("MON", "월", false, false),
        ("TUE", "화", false, false),
        ("WED", "수", false, false),
        ("THU", "목", false, false),
        ("FRI", "금", false, false),
        ("SAT", "토", true,  false),
        ("SUN", "일", true,  true ),
    ];
}

// ─── Server pane: editable day VM ─────────────────────────────────────────────

public sealed partial class OpsHoursDayEditVm : ObservableObject
{
    public string DayLabel  { get; }
    public string DayKey    { get; }
    public bool   IsWeekend { get; }
    public bool   IsSunday  { get; }

    public TimeSpan OriginalOpenTime  { get; private set; }
    public TimeSpan OriginalCloseTime { get; private set; }
    public bool     OriginalIsClosed  { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanged))]
    private TimeSpan openTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanged))]
    private TimeSpan closeTime;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanged))]
    [NotifyPropertyChangedFor(nameof(IsNotClosed))]
    private bool isClosed;

    public bool HasChanged =>
        IsClosed    != OriginalIsClosed  ||
        OpenTime    != OriginalOpenTime  ||
        CloseTime   != OriginalCloseTime;

    public bool IsNotClosed => !IsClosed;

    public OpsHoursDayEditVm(
        string dayLabel, string dayKey, bool isWeekend, bool isSunday,
        TimeSpan openTime, TimeSpan closeTime, bool isClosed)
    {
        DayLabel  = dayLabel;
        DayKey    = dayKey;
        IsWeekend = isWeekend;
        IsSunday  = isSunday;
        this.openTime  = openTime;
        this.closeTime = closeTime;
        this.isClosed  = isClosed;
        OriginalOpenTime  = openTime;
        OriginalCloseTime = closeTime;
        OriginalIsClosed  = isClosed;
    }

    public void CommitCurrent()
    {
        OriginalOpenTime  = OpenTime;
        OriginalCloseTime = CloseTime;
        OriginalIsClosed  = IsClosed;
        OnPropertyChanged(nameof(HasChanged));
    }

    public void ResetToOriginal()
    {
        OpenTime  = OriginalOpenTime;
        CloseTime = OriginalCloseTime;
        IsClosed  = OriginalIsClosed;
    }

    public ushort ToOpenMinutes()  => IsClosed ? (ushort)0 : (ushort)(int)OpenTime.TotalMinutes;
    public ushort ToCloseMinutes() => IsClosed ? (ushort)0 : (ushort)(int)CloseTime.TotalMinutes;
}

// ─── Device pane: read-only day VM ────────────────────────────────────────────

public sealed partial class OpsHoursDayDisplayVm : ObservableObject
{
    public string DayLabel  { get; }
    public string DayKey    { get; }
    public bool   IsWeekend { get; }
    public bool   IsSunday  { get; }

    [ObservableProperty] private string openText  = "--:--";
    [ObservableProperty] private string closeText = "--:--";
    [ObservableProperty] private bool   isClosed;
    [ObservableProperty] private bool   isDiff;
    [ObservableProperty] private bool   hasData;

    public OpsHoursDayDisplayVm(string dayLabel, string dayKey, bool isWeekend, bool isSunday)
    {
        DayLabel  = dayLabel;
        DayKey    = dayKey;
        IsWeekend = isWeekend;
        IsSunday  = isSunday;
    }
}

// ─── Compare result types ─────────────────────────────────────────────────────

public enum DeviceCompareStatus { Match, Diff, Error }

public sealed class OpsCompareDayDiff
{
    public required string DayLabel    { get; init; }
    public required bool   IsWeekend   { get; init; }
    public required bool   IsSunday    { get; init; }
    public required bool   IsDiff      { get; init; }
    public required string ServerValue { get; init; }
    public required string DeviceValue { get; init; }
    public required string DiffFields  { get; init; }
}

public sealed partial class OpsCompareDeviceResultVm : ObservableObject
{
    public int                             DeviceId { get; init; }
    public DeviceCompareStatus             Status   { get; init; }
    public IReadOnlyList<OpsCompareDayDiff> AllDays  { get; init; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronGlyph))]
    private bool isExpanded;

    public int    DiffDayCount  => AllDays.Count(d => d.IsDiff);
    public string DiffDaysText  => string.Join(", ", AllDays.Where(d => d.IsDiff).Select(d => d.DayLabel));
    public bool   HasDiffs      => DiffDayCount > 0;
    public string DeviceIdText  => $"#{DeviceId:D3}";
    public string ChevronGlyph  => IsExpanded ? "" : "";  // ChevronDown / ChevronRight

    public string StatusTag => Status switch
    {
        DeviceCompareStatus.Match => "일치",
        DeviceCompareStatus.Diff  => "불일치",
        _                         => "응답 없음",
    };

    public string SummaryText => Status switch
    {
        DeviceCompareStatus.Match => "모든 요일 동일",
        DeviceCompareStatus.Diff  => $"{DiffDayCount}개 요일 차이 · {DiffDaysText}",
        _                         => "장치 통신 실패 · 타임아웃",
    };

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}

// ─── Main ViewModel ───────────────────────────────────────────────────────────

public sealed partial class OperatingHoursSyncViewModel : ObservableObject
{
    private readonly ISettingsService                _settingsService;
    private readonly IOperatingHoursSource           _source;
    private readonly IOperatingHoursDeviceSyncService _syncService;
    private readonly ISerialService                  _serialService;

    // Countdown timer is managed by the View layer (OperatingHoursSyncPage.xaml.cs)
    // to avoid a WinUI3 runtime dependency in this testable ViewModel.

    // ─ Server pane ──────────────────────────────────────────────────────────────

    [ObservableProperty] private string storeId            = string.Empty;
    [ObservableProperty] private string storeInfoText      = string.Empty;
    [ObservableProperty] private bool   isServerScheduleLoaded;
    [ObservableProperty] private bool   showAutoSyncBadge;
    [ObservableProperty] private string autoSyncBadgeText  = string.Empty;

    public ObservableCollection<OpsHoursDayEditVm> ServerDays { get; } = [];

    // ─ Device pane ──────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceCountText))]
    [NotifyCanExecuteChangedFor(nameof(NavigatePrevCommand))]
    [NotifyCanExecuteChangedFor(nameof(NavigateNextCommand))]
    private int deviceRangeFrom;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceCountText))]
    [NotifyCanExecuteChangedFor(nameof(NavigatePrevCommand))]
    [NotifyCanExecuteChangedFor(nameof(NavigateNextCommand))]
    private int deviceRangeTo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceNavText))]
    [NotifyCanExecuteChangedFor(nameof(NavigatePrevCommand))]
    [NotifyCanExecuteChangedFor(nameof(NavigateNextCommand))]
    private int currentDeviceId;

    [ObservableProperty] private string deviceStatusText  = "읽기 전";
    [ObservableProperty] private string deviceStatusBadge = "idle";  // idle|ok|diff|err

    public string DeviceCountText =>
        $"장치 범위: {DeviceRangeFrom} ~ {DeviceRangeTo} · 총 {Math.Max(0, DeviceRangeTo - DeviceRangeFrom + 1)}대";

    public string DeviceNavText => $"#{CurrentDeviceId:D3}";

    public ObservableCollection<OpsHoursDayDisplayVm> DeviceDays { get; } = [];

    // ─ Batch progress ────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchProgressValue))]
    private int batchDone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchProgressValue))]
    private int batchTotal = 1;

    [ObservableProperty] private string batchLabel        = string.Empty;
    [ObservableProperty] private bool   isBatchInProgress;

    public double BatchProgressValue =>
        BatchTotal > 0 ? (double)BatchDone / BatchTotal * 100.0 : 0;

    // ─ Compare modal ─────────────────────────────────────────────────────────────

    public ObservableCollection<OpsCompareDeviceResultVm> CompareResults { get; } = [];

    [ObservableProperty] private int    compareMatchCount;
    [ObservableProperty] private int    compareDiffCount;
    [ObservableProperty] private int    compareErrCount;
    [ObservableProperty] private string compareFooterText = string.Empty;
    [ObservableProperty] private string compareTitle      = string.Empty;

    public event EventHandler? CompareRequested;
    public event EventHandler<int>? CountdownStartRequested;

    // ─ Status ─────────────────────────────────────────────────────────────────────

    [ObservableProperty] private string statusMessage = string.Empty;

    // ─ Constructor ────────────────────────────────────────────────────────────────

    public OperatingHoursSyncViewModel(
        ISettingsService settingsService,
        IOperatingHoursSource source,
        IOperatingHoursDeviceSyncService syncService,
        ISerialService serialService)
    {
        _settingsService = settingsService;
        _source          = source;
        _syncService     = syncService;
        _serialService   = serialService;

        deviceRangeFrom = settingsService.ScanStartId;
        deviceRangeTo   = settingsService.ScanEndId;
        currentDeviceId = settingsService.ScanStartId;
        storeId         = settingsService.BackendStoreId;

        foreach (var (key, label, isWeekend, isSunday) in OpsDayMeta.All)
        {
            ServerDays.Add(new OpsHoursDayEditVm(
                label, key, isWeekend, isSunday,
                TimeSpan.FromHours(9), TimeSpan.FromHours(22), false));
            DeviceDays.Add(new OpsHoursDayDisplayVm(label, key, isWeekend, isSunday));
        }
    }

    // ─ Partial hooks ──────────────────────────────────────────────────────────────

    partial void OnCurrentDeviceIdChanged(int value)
    {
        _ = LoadCurrentDeviceAsync();
    }

    partial void OnDeviceRangeFromChanged(int value)
    {
        if (CurrentDeviceId < value)
            CurrentDeviceId = value;
    }

    partial void OnDeviceRangeToChanged(int value)
    {
        if (CurrentDeviceId > value)
            CurrentDeviceId = value;
    }

    // ─ Commands ───────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadFromServerAsync()
    {
        StatusMessage = "서버에서 운영 시간을 불러오는 중...";
        var result = await _source.LoadAsync(CancellationToken.None);
        if (!result.Success || result.Schedule is null)
        {
            StatusMessage = result.Message;
            return;
        }

        ApplyScheduleToServerDays(result.Schedule);
        IsServerScheduleLoaded = true;
        StatusMessage = result.FromCache
            ? $"캐시에서 로드됨 · {result.Message}"
            : $"서버에서 로드됨 · {result.Message}";

        RefreshDeviceDiffs();
        CountdownStartRequested?.Invoke(this, 60);
    }

    [RelayCommand]
    private async Task PushToServerAsync()
    {
        StatusMessage = "Saving operating hours to server...";
        var result = await _source.SaveAsync(BuildScheduleFromServerDays(), CancellationToken.None);
        if (!result.Success || result.Schedule is null)
        {
            StatusMessage = result.Message;
            return;
        }

        ApplyScheduleToServerDays(result.Schedule);
        IsServerScheduleLoaded = true;
        StatusMessage = result.Message;
        RefreshDeviceDiffs();
    }

    [RelayCommand(CanExecute = nameof(CanNavigatePrev))]
    private void NavigatePrev() => CurrentDeviceId--;

    private bool CanNavigatePrev() => CurrentDeviceId > DeviceRangeFrom;

    [RelayCommand(CanExecute = nameof(CanNavigateNext))]
    private void NavigateNext() => CurrentDeviceId++;

    private bool CanNavigateNext() => CurrentDeviceId < DeviceRangeTo;

    [RelayCommand]
    private async Task SendToCurrentDeviceAsync()
    {
        var schedule = BuildScheduleFromServerDays();
        StatusMessage = $"#{CurrentDeviceId:D3} 장치에 전송 중...";
        var result = await _serialService.SetOperatingHoursAsync(CurrentDeviceId, schedule);
        StatusMessage = result.Success
            ? $"#{CurrentDeviceId:D3} 전송 완료"
            : $"#{CurrentDeviceId:D3} 전송 실패: {result.Message}";
        _ = LoadCurrentDeviceAsync();
    }

    [RelayCommand]
    private async Task SendToAllDevicesAsync()
    {
        int start = Math.Min(DeviceRangeFrom, DeviceRangeTo);
        int end   = Math.Max(DeviceRangeFrom, DeviceRangeTo);
        int total = end - start + 1;

        BatchLabel        = "일괄 전송";
        BatchDone         = 0;
        BatchTotal        = total;
        IsBatchInProgress = true;

        try
        {
            var schedule = BuildScheduleFromServerDays();
            var results  = await _syncService.SyncRangeAsync(start, end, schedule, CancellationToken.None);
            BatchDone = results.Count;
            int ok = results.Count(r => r.Success);
            StatusMessage = $"일괄 전송 완료 · {ok}/{total} 성공";
        }
        finally
        {
            IsBatchInProgress = false;
        }
    }

    [RelayCommand]
    private async Task CompareCurrentDeviceAsync()
    {
        var raw  = await _serialService.GetOperatingHoursAsync(CurrentDeviceId);
        var item = BuildCompareResult(CurrentDeviceId, raw);
        CompareResults.Clear();
        CompareResults.Add(item);
        if (item.HasDiffs) item.IsExpanded = true;
        UpdateCompareStats(single: true);
        CompareTitle = $"장치 #{CurrentDeviceId:D3} 비교 결과";
        CompareRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task CompareAllDevicesAsync()
    {
        int start = Math.Min(DeviceRangeFrom, DeviceRangeTo);
        int end   = Math.Max(DeviceRangeFrom, DeviceRangeTo);
        int total = end - start + 1;

        BatchLabel        = "일괄 비교";
        BatchDone         = 0;
        BatchTotal        = total;
        IsBatchInProgress = true;
        CompareResults.Clear();

        try
        {
            for (int id = start; id <= end; id++)
            {
                var raw = await _serialService.GetOperatingHoursAsync(id);
                CompareResults.Add(BuildCompareResult(id, raw));
                BatchDone++;
            }
        }
        finally
        {
            IsBatchInProgress = false;
        }

        var firstDiff = CompareResults.FirstOrDefault(r => r.Status == DeviceCompareStatus.Diff);
        if (firstDiff is not null) firstDiff.IsExpanded = true;
        UpdateCompareStats(single: false);
        CompareTitle = "장치 비교 결과 (일괄)";
        CompareRequested?.Invoke(this, EventArgs.Empty);
    }

    // ─ Private helpers ────────────────────────────────────────────────────────────

    private void ApplyScheduleToServerDays(OperatingHoursSchedule schedule)
    {
        StoreId       = schedule.StoreId;
        StoreInfoText = BuildStoreInfoText(schedule);

        var byKey = schedule.Days.ToDictionary(
            d => d.DayOfWeek.ToUpperInvariant(), d => d);

        ServerDays.Clear();
        foreach (var (key, label, isWeekend, isSunday) in OpsDayMeta.All)
        {
            byKey.TryGetValue(key, out var day);
            var openTs  = (day is not null && !day.IsClosed)
                          ? TimeSpan.FromMinutes(day.OpenMinutes)
                          : TimeSpan.FromHours(9);
            var closeTs = (day is not null && !day.IsClosed)
                          ? TimeSpan.FromMinutes(day.CloseMinutes)
                          : TimeSpan.FromHours(22);
            ServerDays.Add(new OpsHoursDayEditVm(
                label, key, isWeekend, isSunday, openTs, closeTs, day?.IsClosed ?? false));
        }
    }

    private async Task LoadCurrentDeviceAsync()
    {
        DeviceStatusText  = "읽는 중...";
        DeviceStatusBadge = "idle";

        var result = await _serialService.GetOperatingHoursAsync(CurrentDeviceId);
        if (!result.Success || result.Schedule is null)
        {
            DeviceStatusText  = "응답 없음";
            DeviceStatusBadge = "err";
            foreach (var d in DeviceDays) { d.HasData = false; d.IsDiff = false; }
            return;
        }

        var byKey    = result.Schedule.Days.ToDictionary(d => d.DayOfWeek.ToUpperInvariant(), d => d);
        bool anyDiff = false;

        foreach (var display in DeviceDays)
        {
            if (!byKey.TryGetValue(display.DayKey, out var deviceDay))
            {
                display.HasData = false;
                display.IsDiff  = false;
                continue;
            }

            display.HasData   = true;
            display.IsClosed  = deviceDay.IsClosed;
            display.OpenText  = deviceDay.IsClosed ? "휴무" : FormatMinutes(deviceDay.OpenMinutes);
            display.CloseText = deviceDay.IsClosed ? "--:--" : FormatMinutes(deviceDay.CloseMinutes);

            var server = ServerDays.FirstOrDefault(s => s.DayKey == display.DayKey);
            if (server is not null)
            {
                display.IsDiff = ComputeIsDiff(server, deviceDay);
                if (display.IsDiff) anyDiff = true;
            }
        }

        DeviceStatusText  = anyDiff ? "불일치" : "일치";
        DeviceStatusBadge = anyDiff ? "diff"   : "ok";
    }

    private void RefreshDeviceDiffs()
    {
        foreach (var display in DeviceDays)
        {
            if (!display.HasData) continue;
            var server = ServerDays.FirstOrDefault(s => s.DayKey == display.DayKey);
            if (server is null) { display.IsDiff = false; continue; }

            ushort deviceOpen  = ParseTime(display.OpenText);
            ushort deviceClose = ParseTime(display.CloseText);
            bool closedDiff    = display.IsClosed != server.IsClosed;
            bool openDiff      = !display.IsClosed && server.ToOpenMinutes()  != deviceOpen;
            bool closeDiff     = !display.IsClosed && server.ToCloseMinutes() != deviceClose;
            display.IsDiff     = closedDiff || openDiff || closeDiff;
        }
    }

    private OpsCompareDeviceResultVm BuildCompareResult(int deviceId, OperatingHoursDeviceReadResult result)
    {
        if (!result.Success || result.Schedule is null)
        {
            return new OpsCompareDeviceResultVm
            {
                DeviceId = deviceId,
                Status   = DeviceCompareStatus.Error,
                AllDays  = OpsDayMeta.All.Select(m => new OpsCompareDayDiff
                {
                    DayLabel    = m.Label,
                    IsWeekend   = m.IsWeekend,
                    IsSunday    = m.IsSunday,
                    IsDiff      = false,
                    ServerValue = ServerDayValue(m.Key),
                    DeviceValue = "—",
                    DiffFields  = string.Empty,
                }).ToArray(),
            };
        }

        var byKey    = result.Schedule.Days.ToDictionary(d => d.DayOfWeek.ToUpperInvariant(), d => d);
        bool anyDiff = false;

        var allDays = OpsDayMeta.All.Select(m =>
        {
            var server = ServerDays.FirstOrDefault(s => s.DayKey == m.Key);
            byKey.TryGetValue(m.Key, out var device);

            string serverVal = server is null  ? "—"
                : server.IsClosed              ? "휴무"
                : $"{server.OpenTime:hh\\:mm} – {server.CloseTime:hh\\:mm}";

            string deviceVal = device is null  ? "—"
                : device.IsClosed              ? "휴무"
                : $"{FormatMinutes(device.OpenMinutes)} – {FormatMinutes(device.CloseMinutes)}";

            var diffFields = new List<string>();
            if (server is not null && device is not null)
            {
                if (server.IsClosed != device.IsClosed)
                {
                    diffFields.Add("휴무여부");
                }
                else if (!server.IsClosed && !device.IsClosed)
                {
                    if (server.ToOpenMinutes()  != device.OpenMinutes)  diffFields.Add("오픈");
                    if (server.ToCloseMinutes() != device.CloseMinutes) diffFields.Add("마감");
                }
            }

            bool isDiff = diffFields.Count > 0;
            if (isDiff) anyDiff = true;

            return new OpsCompareDayDiff
            {
                DayLabel    = m.Label,
                IsWeekend   = m.IsWeekend,
                IsSunday    = m.IsSunday,
                IsDiff      = isDiff,
                ServerValue = serverVal,
                DeviceValue = deviceVal,
                DiffFields  = string.Join(", ", diffFields),
            };
        }).ToArray();

        return new OpsCompareDeviceResultVm
        {
            DeviceId = deviceId,
            Status   = anyDiff ? DeviceCompareStatus.Diff : DeviceCompareStatus.Match,
            AllDays  = allDays,
        };
    }

    private void UpdateCompareStats(bool single)
    {
        CompareMatchCount = CompareResults.Count(r => r.Status == DeviceCompareStatus.Match);
        CompareDiffCount  = CompareResults.Count(r => r.Status == DeviceCompareStatus.Diff);
        CompareErrCount   = CompareResults.Count(r => r.Status == DeviceCompareStatus.Error);
        int total         = CompareResults.Count;

        if (!single)
        {
            CompareFooterText =
                $"{CompareMatchCount} / {total} 일치 · {CompareDiffCount} 불일치"
                + (CompareErrCount > 0 ? $" · {CompareErrCount} 응답없음" : string.Empty);
        }
        else if (total == 1)
        {
            var r = CompareResults[0];
            CompareFooterText = r.Status switch
            {
                DeviceCompareStatus.Diff  => $"{r.DiffDayCount}개 요일에서 차이가 발견되었습니다.",
                DeviceCompareStatus.Match => "장치 스케쥴이 서버와 완전히 일치합니다.",
                _                         => "장치 통신에 실패했습니다.",
            };
        }
    }

    private OperatingHoursSchedule BuildScheduleFromServerDays()
    {
        var days = ServerDays
            .Select(d => new OperatingHoursDay(d.DayKey, d.ToOpenMinutes(), d.ToCloseMinutes()))
            .ToArray();
        uint checksum = OperatingHoursSchedule.ComputeChecksum(null, null, days);
        return new OperatingHoursSchedule(StoreId, null, null, null, days, checksum);
    }

    private string ServerDayValue(string dayKey)
    {
        var d = ServerDays.FirstOrDefault(s => s.DayKey == dayKey);
        if (d is null) return "—";
        return d.IsClosed ? "휴무" : $"{d.OpenTime:hh\\:mm} – {d.CloseTime:hh\\:mm}";
    }

    private static bool ComputeIsDiff(OpsHoursDayEditVm server, OperatingHoursDay device)
    {
        if (server.IsClosed != device.IsClosed) return true;
        if (server.IsClosed && device.IsClosed) return false;
        return server.ToOpenMinutes()  != device.OpenMinutes
            || server.ToCloseMinutes() != device.CloseMinutes;
    }

    private static string BuildStoreInfoText(OperatingHoursSchedule s)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(s.StoreName))  parts.Add(s.StoreName);
        if (!string.IsNullOrWhiteSpace(s.Timezone))   parts.Add(s.Timezone);
        if (!string.IsNullOrWhiteSpace(s.ModifiedAt)) parts.Add($"수정 {s.ModifiedAt}");
        return string.Join(" · ", parts);
    }

    private static string FormatMinutes(ushort minutes)
        => TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    private static ushort ParseTime(string text)
    {
        if (TimeSpan.TryParseExact(text, @"hh\:mm", CultureInfo.InvariantCulture, out var ts))
            return (ushort)(int)ts.TotalMinutes;
        return 0;
    }

}
