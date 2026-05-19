using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AnimatronicsControlCenter.UI.ViewModels;

public enum OperatingHoursDeviceSyncStatus
{
    NotSent,
    Synced,
    Mismatch,
    Failed,
}

public sealed partial class OperatingHoursDeviceResultViewModel : ObservableObject
{
    public OperatingHoursDeviceResultViewModel(int deviceId)
    {
        DeviceId = deviceId;
    }

    public int DeviceId { get; }

    [ObservableProperty]
    private OperatingHoursDeviceSyncStatus writeStatus = OperatingHoursDeviceSyncStatus.NotSent;

    [ObservableProperty]
    private OperatingHoursDeviceSyncStatus readStatus = OperatingHoursDeviceSyncStatus.NotSent;

    [ObservableProperty]
    private uint deviceChecksum;

    [ObservableProperty]
    private string message = string.Empty;
}

public sealed record OperatingHoursDayViewModel(
    string DayOfWeek,
    string StatusText,
    string TimeRangeText,
    string OpenText,
    string CloseText,
    string StatusGlyph)
{
    public static OperatingHoursDayViewModel FromDay(OperatingHoursDay day)
    {
        string openText = FormatMinutes(day.OpenMinutes);
        string closeText = FormatMinutes(day.CloseMinutes);
        return day.IsClosed
            ? new OperatingHoursDayViewModel(day.DayOfWeek, "Closed", "Closed", "--:--", "--:--", "\uE711")
            : new OperatingHoursDayViewModel(day.DayOfWeek, "Open", $"{openText} - {closeText}", openText, closeText, "\uE73E");
    }

    private static string FormatMinutes(ushort minutes)
        => TimeSpan.FromMinutes(minutes).ToString(@"hh\:mm", CultureInfo.InvariantCulture);
}

public sealed partial class OperatingHoursSyncViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IOperatingHoursSource _source;
    private readonly IOperatingHoursDeviceSyncService _syncService;
    private readonly ISerialService _serialService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StoreNameText))]
    [NotifyPropertyChangedFor(nameof(StoreIdText))]
    [NotifyPropertyChangedFor(nameof(ServerTimezoneText))]
    [NotifyPropertyChangedFor(nameof(ModifiedAtText))]
    private OperatingHoursSchedule? schedule;

    [ObservableProperty]
    private int startDeviceId;

    [ObservableProperty]
    private int endDeviceId;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private string timezoneWarningText = string.Empty;

    public ObservableCollection<OperatingHoursDeviceResultViewModel> DeviceResults { get; } = new();
    public ObservableCollection<OperatingHoursDayViewModel> ScheduleDays { get; } = new();

    public string StoreNameText => Schedule?.StoreName ?? string.Empty;
    public string StoreIdText => string.IsNullOrWhiteSpace(Schedule?.StoreId)
        ? _settingsService.BackendStoreId
        : Schedule.StoreId;
    public string ServerTimezoneText => Schedule?.Timezone ?? string.Empty;
    public string ModifiedAtText => Schedule?.ModifiedAt ?? string.Empty;
    public string CurrentTimezoneOffsetText => FormatOffset(_settingsService.PingUtcOffsetMinutes);

    public OperatingHoursSyncViewModel(
        ISettingsService settingsService,
        IOperatingHoursSource source,
        IOperatingHoursDeviceSyncService syncService,
        ISerialService serialService)
    {
        _settingsService = settingsService;
        _source = source;
        _syncService = syncService;
        _serialService = serialService;
        StartDeviceId = settingsService.ScanStartId;
        EndDeviceId = settingsService.ScanEndId;
    }

    partial void OnScheduleChanged(OperatingHoursSchedule? value)
    {
        TimezoneWarningText = BuildTimezoneWarning(value);
        ScheduleDays.Clear();
        if (value is null)
        {
            return;
        }

        foreach (var day in value.Days)
        {
            ScheduleDays.Add(OperatingHoursDayViewModel.FromDay(day));
        }
    }

    [RelayCommand]
    private async Task LoadScheduleAsync()
    {
        var result = await _source.LoadAsync(CancellationToken.None);
        Schedule = result.Schedule;
        StatusMessage = result.Message;
    }

    [RelayCommand]
    private async Task SyncAsync()
    {
        if (Schedule is null)
        {
            StatusMessage = "Operating hours are not loaded.";
            return;
        }

        DeviceResults.Clear();
        var results = await _syncService.SyncRangeAsync(StartDeviceId, EndDeviceId, Schedule, CancellationToken.None);
        foreach (var result in results)
        {
            DeviceResults.Add(new OperatingHoursDeviceResultViewModel(result.DeviceId)
            {
                WriteStatus = result.Success ? OperatingHoursDeviceSyncStatus.Synced : OperatingHoursDeviceSyncStatus.Failed,
                DeviceChecksum = result.Checksum,
                Message = result.Message,
            });
        }
    }

    [RelayCommand]
    private async Task ReadAndCompareAsync()
    {
        if (Schedule is null)
        {
            StatusMessage = "Operating hours are not loaded.";
            return;
        }

        DeviceResults.Clear();
        int start = Math.Min(StartDeviceId, EndDeviceId);
        int end = Math.Max(StartDeviceId, EndDeviceId);
        for (int deviceId = start; deviceId <= end; deviceId++)
        {
            var result = await _serialService.GetOperatingHoursAsync(deviceId);
            var status = OperatingHoursDeviceSyncStatus.Failed;
            if (result.Success && result.Schedule is not null)
            {
                status = result.Schedule.Checksum == Schedule.Checksum
                    ? OperatingHoursDeviceSyncStatus.Synced
                    : OperatingHoursDeviceSyncStatus.Mismatch;
            }

            DeviceResults.Add(new OperatingHoursDeviceResultViewModel(deviceId)
            {
                ReadStatus = status,
                DeviceChecksum = result.Schedule?.Checksum ?? 0,
                Message = result.Message,
            });
        }
    }

    private string BuildTimezoneWarning(OperatingHoursSchedule? loadedSchedule)
    {
        if (loadedSchedule?.Timezone == "Asia/Seoul" && _settingsService.PingUtcOffsetMinutes != 540)
        {
            return $"Server timezone is Asia/Seoul, but current settings offset is {FormatOffset(_settingsService.PingUtcOffsetMinutes)}.";
        }

        return string.Empty;
    }

    private static string FormatOffset(int offsetMinutes)
    {
        char sign = offsetMinutes < 0 ? '-' : '+';
        int absolute = Math.Abs(offsetMinutes);
        return $"UTC{sign}{absolute / 60:00}:{absolute % 60:00}";
    }
}
