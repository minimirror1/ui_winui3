using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Motors;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Protocol;
using Microsoft.UI.Dispatching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
                _ = LoadFileContentAsync(value.Path);
            else
                FileContent = string.Empty;
        }

        // ── 모터 이동 ─────────────────────────────────────────────────

        [RelayCommand]
        private async Task MoveMotorAsync(MotorState motor)
        {
            if (SelectedDevice == null || motor == null) return;

            var packet = BinarySerializer.EncodeMove(
                BinaryProtocolConst.HostId,
                (byte)SelectedDevice.Id,
                (byte)motor.Id,
                motor.Position);

            await _serialService.SendBinaryCommandAsync(SelectedDevice.Id, packet);
        }

        // ── 모터 폴링 ─────────────────────────────────────────────────

        public void SetMotorsPollingAllowed(bool allowed)
        {
            _isMotorsPollingAllowed = allowed;
            EnsureMotorsPollingState();
        }

        public void StopMotorsPolling()
        {
            try { _motorsPollingCts?.Cancel(); }
            catch { }
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
            _motorsPollingTask = RunMotorsPollingLoopAsync(_motorsPollingCts.Token);
        }

        private async Task RunMotorsPollingLoopAsync(CancellationToken token)
        {
            await RefreshMotorsOnceAsync(token);
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(MotorPollingIntervalMs));
            while (await timer.WaitForNextTickAsync(token))
                await RefreshMotorsOnceAsync(token);
        }

        [RelayCommand]
        private async Task RefreshMotorsAsync()
        {
            await RefreshMotorsOnceAsync(CancellationToken.None);
        }

        // ── GET_MOTORS ────────────────────────────────────────────────

        private async Task LoadMotorsAsync()
        {
            if (SelectedDevice == null) return;

            var responseBytes = await _serialService.SendBinaryQueryAsync(SelectedDevice.Id, BinaryCommand.GetMotors);
            if (responseBytes == null) return;
            if (!BinaryDeserializer.TryParseResponseHeader(responseBytes, out var hdr)) return;
            if (!BinaryDeserializer.IsOk(hdr)) return;

            // async 메서드에서 ReadOnlySpan 불가 → byte[] 슬라이스 사용
            int start    = BinaryProtocolConst.ResponseHeaderSize;
            var payload  = responseBytes[start..(start + hdr.PayloadLen)];
            var patches  = BinaryDeserializer.ParseGetMotorsResponse(payload);
            if (patches.Count == 0) return;

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (SelectedDevice == null) return;
                MotorStateMerger.Apply(SelectedDevice.Motors, patches);
            });
        }

        // ── GET_MOTOR_STATE ───────────────────────────────────────────

        private async Task RefreshMotorsOnceAsync(CancellationToken token)
        {
            if (SelectedDevice == null) return;
            token.ThrowIfCancellationRequested();

            var responseBytes = await _serialService.SendBinaryQueryAsync(SelectedDevice.Id, BinaryCommand.GetMotorState);
            if (responseBytes == null) return;
            if (!BinaryDeserializer.TryParseResponseHeader(responseBytes, out var hdr)) return;
            if (!BinaryDeserializer.IsOk(hdr)) return;

            int start   = BinaryProtocolConst.ResponseHeaderSize;
            var payload = responseBytes[start..(start + hdr.PayloadLen)];
            var patches = BinaryDeserializer.ParseMotorStateResponse(payload);
            if (patches.Count == 0) return;

            _dispatcherQueue.TryEnqueue(() =>
            {
                if (SelectedDevice == null) return;
                MotorStateMerger.Apply(SelectedDevice.Motors, patches);
            });
        }

        // ── 모션 제어 ─────────────────────────────────────────────────

        [RelayCommand]
        private async Task PlayMotionAsync()
        {
            if (SelectedDevice == null) return;
            var packet = BinarySerializer.EncodeMotionCtrl(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, BinaryMotionAction.Play);
            await _serialService.SendBinaryCommandAsync(SelectedDevice.Id, packet);
            SelectedDevice.MotionState = MotionState.Playing;
        }

        [RelayCommand]
        private async Task StopMotionAsync()
        {
            if (SelectedDevice == null) return;
            var packet = BinarySerializer.EncodeMotionCtrl(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, BinaryMotionAction.Stop);
            await _serialService.SendBinaryCommandAsync(SelectedDevice.Id, packet);
            SelectedDevice.MotionState = MotionState.Stopped;
        }

        [RelayCommand]
        private async Task PauseMotionAsync()
        {
            if (SelectedDevice == null) return;
            var packet = BinarySerializer.EncodeMotionCtrl(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, BinaryMotionAction.Pause);
            await _serialService.SendBinaryCommandAsync(SelectedDevice.Id, packet);
            SelectedDevice.MotionState = MotionState.Paused;
        }

        [RelayCommand]
        private async Task SeekMotionAsync(double seconds)
        {
            if (SelectedDevice == null) return;
            var time = TimeSpan.FromSeconds(seconds);
            SelectedDevice.MotionCurrentTime = time;
            var packet = BinarySerializer.EncodeMotionCtrl(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, BinaryMotionAction.Seek, time.TotalSeconds);
            await _serialService.SendBinaryCommandAsync(SelectedDevice.Id, packet);
        }

        public string FormatTime(TimeSpan time) => time.ToString(@"hh\:mm\:ss");

        // ── GET_FILES ─────────────────────────────────────────────────

        [RelayCommand]
        private async Task RefreshFilesAsync()
        {
            if (SelectedDevice == null) return;
            IsFileLoading = true;
            try
            {
                var responseBytes = await _serialService.SendBinaryQueryAsync(SelectedDevice.Id, BinaryCommand.GetFiles);
                if (responseBytes == null) return;
                if (!BinaryDeserializer.TryParseResponseHeader(responseBytes, out var hdr)) return;
                if (!BinaryDeserializer.IsOk(hdr)) return;

                int start    = BinaryProtocolConst.ResponseHeaderSize;
                var payload  = responseBytes[start..(start + hdr.PayloadLen)];
                var entries  = BinaryDeserializer.ParseGetFilesResponse(payload);
                var rootItems = BuildFileTreeFromFlatList(entries);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    Files = new ObservableCollection<FileSystemItem>(rootItems);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in RefreshFilesAsync: {ex.Message}");
                _dispatcherQueue.TryEnqueue(() => Files = new ObservableCollection<FileSystemItem>());
            }
            finally
            {
                IsFileLoading = false;
            }
        }

        // ── GET_FILE ──────────────────────────────────────────────────

        [RelayCommand]
        private async Task LoadFileContentAsync(string path)
        {
            if (SelectedDevice == null) return;
            IsFileLoading = true;
            try
            {
                var packet        = BinarySerializer.EncodeGetFile(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, path);
                var responseBytes = await _serialService.SendBinaryQueryAsync(SelectedDevice.Id, BinaryCommand.GetFile, packet);
                if (responseBytes == null) return;
                if (!BinaryDeserializer.TryParseResponseHeader(responseBytes, out var hdr)) return;
                if (!BinaryDeserializer.IsOk(hdr)) return;

                int start         = BinaryProtocolConst.ResponseHeaderSize;
                var payload       = responseBytes[start..(start + hdr.PayloadLen)];
                var (_, content)  = BinaryDeserializer.ParseGetFileResponse(payload);
                FileContent       = content;
            }
            finally
            {
                IsFileLoading = false;
            }
        }

        // ── SAVE_FILE ─────────────────────────────────────────────────

        [RelayCommand]
        private async Task SaveFileAsync()
        {
            if (SelectedDevice == null || SelectedFile == null) return;
            var packet = BinarySerializer.EncodeSaveFile(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, SelectedFile.Path, FileContent);
            await _serialService.SendBinaryCommandAsync(SelectedDevice.Id, packet);
        }

        // ── VERIFY_FILE ───────────────────────────────────────────────

        [RelayCommand]
        private async Task VerifyFileAsync()
        {
            if (SelectedDevice == null || SelectedFile == null) return;

            var packet        = BinarySerializer.EncodeVerifyFile(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, SelectedFile.Path, FileContent);
            var responseBytes = await _serialService.SendBinaryQueryAsync(SelectedDevice.Id, BinaryCommand.VerifyFile, packet);
            if (responseBytes == null) return;
            if (!BinaryDeserializer.TryParseResponseHeader(responseBytes, out var hdr)) return;
            if (!BinaryDeserializer.IsOk(hdr)) return;

            int payloadStart = BinaryProtocolConst.ResponseHeaderSize;
            var payload      = responseBytes[payloadStart..(payloadStart + hdr.PayloadLen)];
            bool match       = BinaryDeserializer.ParseVerifyFileResponse(payload);

            VerificationResult = match ? "Content Matches Device" : "Content Mismatch";
            IsVerificationDialogOpen = true;
        }

        [RelayCommand]
        private void CloseVerificationDialog()
        {
            IsVerificationDialogOpen = false;
        }

        // ── GET_FILES flat list → 트리 재구성 ────────────────────────

        private static List<FileSystemItem> BuildFileTreeFromFlatList(List<BinaryDeserializer.FileEntry> entries)
        {
            var nodes = entries.Select(e => new FileSystemItem
            {
                Name        = e.Name,
                Path        = e.Path,
                IsDirectory = e.IsDirectory,
                Size        = e.Size,
            }).ToList();

            var roots = new List<FileSystemItem>();
            for (int i = 0; i < nodes.Count; i++)
            {
                int parentIdx = entries[i].ParentIndex;
                if (parentIdx < 0)
                    roots.Add(nodes[i]);
                else if (parentIdx < nodes.Count)
                    nodes[parentIdx].Children.Add(nodes[i]);
                else
                    roots.Add(nodes[i]);
            }
            return roots;
        }
    }
}
