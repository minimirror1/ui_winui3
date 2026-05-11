using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.UI.ViewModels;

public sealed class ServerTrafficEntryViewModel
{
    public ServerTrafficEntryViewModel(BackendTrafficEntry entry)
    {
        Time = entry.Timestamp.ToString("HH:mm:ss.fff");
        Phase = entry.Phase.ToString();
        Method = entry.Method.Method;
        Path = entry.Path;
        StatusCode = entry.StatusCode?.ToString() ?? "-";
        Duration = entry.Duration.HasValue ? $"{entry.Duration.Value.TotalMilliseconds:0} ms" : "-";
        Message = entry.Message;
    }

    public string Time { get; }
    public string Phase { get; }
    public string Method { get; }
    public string Path { get; }
    public string StatusCode { get; }
    public string Duration { get; }
    public string Message { get; }
}

public sealed class ServerMonitorViewModel : INotifyPropertyChanged
{
    private readonly IBackendTrafficTap _trafficTap;
    private readonly ISettingsService _settingsService;
    private string _serverUrl = string.Empty;
    private string _connectionStatus = "Offline";
    private string _lastSuccess = "-";
    private string _lastFailure = "-";
    private string _latestError = "-";

    public ServerMonitorViewModel(IBackendTrafficTap trafficTap, ISettingsService settingsService)
    {
        _trafficTap = trafficTap;
        _settingsService = settingsService;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ServerUrl
    {
        get => _serverUrl;
        private set => SetProperty(ref _serverUrl, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        private set => SetProperty(ref _connectionStatus, value);
    }

    public string LastSuccess
    {
        get => _lastSuccess;
        private set => SetProperty(ref _lastSuccess, value);
    }

    public string LastFailure
    {
        get => _lastFailure;
        private set => SetProperty(ref _lastFailure, value);
    }

    public string LatestError
    {
        get => _latestError;
        private set => SetProperty(ref _latestError, value);
    }

    public ObservableCollection<ServerTrafficEntryViewModel> TrafficEntries { get; } = new();

    public void Refresh(DateTimeOffset now)
    {
        var snapshot = _trafficTap.GetSnapshot(now);
        ServerUrl = _settingsService.BackendBaseUrl;
        ConnectionStatus = snapshot.IsServerOnline ? "Online" : "Offline";
        LastSuccess = snapshot.LastSuccessAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        LastFailure = snapshot.LastFailureAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
        LatestError = string.IsNullOrWhiteSpace(snapshot.LastErrorMessage) ? "-" : snapshot.LastErrorMessage;

        TrafficEntries.Clear();
        foreach (BackendTrafficEntry entry in _trafficTap.GetEntries())
        {
            TrafficEntries.Add(new ServerTrafficEntryViewModel(entry));
        }
    }

    private void SetProperty(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
