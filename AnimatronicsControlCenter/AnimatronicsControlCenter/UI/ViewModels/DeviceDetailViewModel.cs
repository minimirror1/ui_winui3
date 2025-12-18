using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Motors;
using AnimatronicsControlCenter.Core.Models;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.Text.Json.Nodes;
using System.Linq;
using Microsoft.UI.Dispatching;
using System.Threading;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public partial class DeviceDetailViewModel : ObservableObject
    {
        private readonly ISerialService _serialService;
        private readonly DispatcherQueue _dispatcherQueue;

        private bool _isMotorsPollingAllowed;
        private CancellationTokenSource? _motorsPollingCts;
        private Task? _motorsPollingTask;

        [ObservableProperty]
        private Device? selectedDevice;

        [ObservableProperty]
        private ObservableCollection<FileSystemItem> files = new();

        [ObservableProperty]
        private FileSystemItem? selectedFile;

        [ObservableProperty]
        private string fileContent = string.Empty;

        [ObservableProperty]
        private bool isFileLoading;

        [ObservableProperty]
        private string verificationResult = string.Empty;

        [ObservableProperty]
        private bool isVerificationDialogOpen;

        public ObservableCollection<int> MotorPollingIntervals { get; } = new() { 250, 500, 1000, 2000, 5000 };

        [ObservableProperty]
        private bool isMotorPollingEnabled;

        [ObservableProperty]
        private int motorPollingIntervalMs = 1000;

        public DeviceDetailViewModel(ISerialService serialService)
        {
            _serialService = serialService;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        partial void OnSelectedDeviceChanged(Device? value)
        {
            if (value != null)
            {
                // Refresh data when device is selected
                _ = RefreshFilesAsync();
                _ = LoadMotorsAsync();
                EnsureMotorsPollingState();
            }
            else
            {
                StopMotorsPolling();
            }
        }

        partial void OnSelectedFileChanged(FileSystemItem? value)
        {
            if (value != null && !value.IsDirectory)
            {
                _ = LoadFileContentAsync(value.Path);
            }
            else
            {
                FileContent = string.Empty;
            }
        }

        [RelayCommand]
        private async Task MoveMotorAsync(MotorState motor)
        {
             if (SelectedDevice != null && motor != null)
             {
                 await _serialService.SendCommandAsync(SelectedDevice.Id, "move", new { motorId = motor.Id, pos = motor.Position });
             }
        }

        public void SetMotorsPollingAllowed(bool allowed)
        {
            _isMotorsPollingAllowed = allowed;
            EnsureMotorsPollingState();
        }

        public void StopMotorsPolling()
        {
            try
            {
                _motorsPollingCts?.Cancel();
            }
            catch
            {
                // ignore
            }
            finally
            {
                _motorsPollingCts?.Dispose();
                _motorsPollingCts = null;
                _motorsPollingTask = null;
            }
        }

        partial void OnIsMotorPollingEnabledChanged(bool value)
        {
            EnsureMotorsPollingState();
        }

        partial void OnMotorPollingIntervalMsChanged(int value)
        {
            if (value < 100)
            {
                MotorPollingIntervalMs = 100;
                return;
            }
            EnsureMotorsPollingState(restartIfRunning: true);
        }

        private void EnsureMotorsPollingState(bool restartIfRunning = false)
        {
            if (SelectedDevice == null || !IsMotorPollingEnabled || !_isMotorsPollingAllowed)
            {
                StopMotorsPolling();
                return;
            }

            if (_motorsPollingTask != null && !_motorsPollingTask.IsCompleted)
            {
                if (!restartIfRunning) return;
                StopMotorsPolling();
            }

            _motorsPollingCts = new CancellationTokenSource();
            var token = _motorsPollingCts.Token;
            _motorsPollingTask = RunMotorsPollingLoopAsync(token);
        }

        private async Task RunMotorsPollingLoopAsync(CancellationToken token)
        {
            // Immediate refresh, then periodic.
            await RefreshMotorsOnceAsync(token);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(MotorPollingIntervalMs));
            while (await timer.WaitForNextTickAsync(token))
            {
                await RefreshMotorsOnceAsync(token);
            }
        }

        [RelayCommand]
        private async Task RefreshMotorsAsync()
        {
            await RefreshMotorsOnceAsync(CancellationToken.None);
        }

        private async Task LoadMotorsAsync()
        {
            if (SelectedDevice == null) return;

            var response = await _serialService.SendQueryAsync(SelectedDevice.Id, "get_motors");
            if (string.IsNullOrWhiteSpace(response)) return;

            var patches = TryParseMotorPatches(response);
            if (patches == null) return;

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (SelectedDevice == null) return;
                MotorStateMerger.Apply(SelectedDevice.Motors, patches);
            });
        }

        private async Task RefreshMotorsOnceAsync(CancellationToken token)
        {
            if (SelectedDevice == null) return;
            token.ThrowIfCancellationRequested();

            var response = await _serialService.SendQueryAsync(SelectedDevice.Id, "get_motor_state");
            if (string.IsNullOrWhiteSpace(response)) return;

            var patches = TryParseMotorPatches(response);
            if (patches == null) return;

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (SelectedDevice == null) return;
                MotorStateMerger.Apply(SelectedDevice.Motors, patches);
            });
        }

        private static List<MotorStatePatch>? TryParseMotorPatches(string responseJson)
        {
            try
            {
                var json = JsonNode.Parse(responseJson);
                if (json == null) return null;
                if (json["status"]?.ToString() != "ok") return null;

                var motorsNode = json["payload"]?["motors"] as JsonArray;
                if (motorsNode == null) return null;

                List<MotorStatePatch> patches = new();
                foreach (var node in motorsNode)
                {
                    if (node is not JsonObject m) continue;
                    int id = m["id"]?.GetValue<int>() ?? 0;
                    if (id <= 0) continue;

                    patches.Add(new MotorStatePatch
                    {
                        Id = id,
                        GroupId = m["groupId"]?.GetValue<int?>(),
                        SubId = m["subId"]?.GetValue<int?>(),
                        Type = m["type"]?.GetValue<string?>(),
                        Status = m["status"]?.GetValue<string?>(),
                        Position = m["position"]?.GetValue<double?>(),
                        Velocity = m["velocity"]?.GetValue<double?>()
                    });
                }

                return patches;
            }
            catch
            {
                return null;
            }
        }

        [RelayCommand]
        private async Task PlayMotionAsync()
        {
            if (SelectedDevice == null) return;
            await _serialService.SendCommandAsync(SelectedDevice.Id, "motion_ctrl", new { action = "play" });
            SelectedDevice.MotionState = MotionState.Playing;
        }

        [RelayCommand]
        private async Task StopMotionAsync()
        {
            if (SelectedDevice == null) return;
            await _serialService.SendCommandAsync(SelectedDevice.Id, "motion_ctrl", new { action = "stop" });
            SelectedDevice.MotionState = MotionState.Stopped;
        }

        [RelayCommand]
        private async Task PauseMotionAsync()
        {
            if (SelectedDevice == null) return;
            await _serialService.SendCommandAsync(SelectedDevice.Id, "motion_ctrl", new { action = "pause" });
            SelectedDevice.MotionState = MotionState.Paused;
        }

        [RelayCommand]
        private async Task SeekMotionAsync(double seconds)
        {
            if (SelectedDevice == null) return;
            var time = TimeSpan.FromSeconds(seconds);
            SelectedDevice.MotionCurrentTime = time;
            await _serialService.SendCommandAsync(SelectedDevice.Id, "motion_ctrl", new { action = "seek", time = time.TotalSeconds });
        }

        public string FormatTime(TimeSpan time)
        {
            return time.ToString(@"hh\:mm\:ss");
        }

        [RelayCommand]
        private async Task RefreshFilesAsync()
        {
            if (SelectedDevice == null) return;

            IsFileLoading = true;
            try
            {
                var response = await _serialService.SendQueryAsync(SelectedDevice.Id, "get_files");
                if (!string.IsNullOrEmpty(response))
                {
                    var json = JsonNode.Parse(response);
                    if (json != null && json["status"]?.ToString() == "ok")
                    {
                        var payload = json["payload"];
                        if (payload != null)
                        {
                            var items = JsonSerializer.Deserialize<List<FileSystemItem>>(payload.ToString(), new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            });

                            if (items != null)
                            {
                                Files = new ObservableCollection<FileSystemItem>(items);
                            }
                            else
                            {
                                Files = new ObservableCollection<FileSystemItem>();
                            }
                        }
                    }
                }
            }
            finally
            {
                IsFileLoading = false;
            }
        }

        [RelayCommand]
        private async Task LoadFileContentAsync(string path)
        {
            if (SelectedDevice == null) return;
            IsFileLoading = true;
            try
            {
                var response = await _serialService.SendQueryAsync(SelectedDevice.Id, "get_file", new { path });
                if (!string.IsNullOrEmpty(response))
                {
                    var json = JsonNode.Parse(response);
                    if (json != null && json["status"]?.ToString() == "ok")
                    {
                        FileContent = json["payload"]?["content"]?.ToString() ?? "";
                    }
                }
            }
            finally
            {
                IsFileLoading = false;
            }
        }

        [RelayCommand]
        private async Task SaveFileAsync()
        {
            if (SelectedDevice == null || SelectedFile == null) return;
            
            await _serialService.SendCommandAsync(SelectedDevice.Id, "save_file", new { path = SelectedFile.Path, content = FileContent });
        }

        [RelayCommand]
        private async Task VerifyFileAsync()
        {
            if (SelectedDevice == null || SelectedFile == null) return;

            var response = await _serialService.SendQueryAsync(SelectedDevice.Id, "verify_file", new { path = SelectedFile.Path, content = FileContent });
            if (!string.IsNullOrEmpty(response))
            {
                 var json = JsonNode.Parse(response);
                 if (json != null && json["status"]?.ToString() == "ok")
                 {
                     bool match = json["payload"]?["match"]?.GetValue<bool>() ?? false;
                     VerificationResult = match ? "Content Matches Device" : "Content Mismatch";
                     IsVerificationDialogOpen = true;
                 }
            }
        }
        
        [RelayCommand]
        private void CloseVerificationDialog()
        {
            IsVerificationDialogOpen = false;
        }
    }
}
