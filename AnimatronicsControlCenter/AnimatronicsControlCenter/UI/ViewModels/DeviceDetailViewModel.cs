using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Collections.Generic;
using System;
using System.Text.Json.Nodes;
using System.Linq;

namespace AnimatronicsControlCenter.UI.ViewModels
{
    public partial class DeviceDetailViewModel : ObservableObject
    {
        private readonly ISerialService _serialService;

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

        public DeviceDetailViewModel(ISerialService serialService)
        {
            _serialService = serialService;
        }

        partial void OnSelectedDeviceChanged(Device? value)
        {
            if (value != null)
            {
                // Refresh data when device is selected
                _ = RefreshFilesAsync();
                // Also could refresh motion info here
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
