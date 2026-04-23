using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Motors;
using AnimatronicsControlCenter.Core.Protocol;
using AnimatronicsControlCenter.Core.Utilities;
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
        private CancellationTokenSource? _statusPollingCts;
        private Task? _statusPollingTask;
        private CancellationTokenSource? _deviceLoadCts;

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

        [ObservableProperty]
        private bool isInitialLoadInProgress;

        [ObservableProperty]
        private string filesStatusMessage = string.Empty;

        [ObservableProperty]
        private string motorsStatusMessage = string.Empty;

        [ObservableProperty]
        private string lastLoadError = string.Empty;

        public ObservableCollection<int> MotorPollingIntervals { get; } = new() { 250, 500, 1000, 2000, 5000 };

        [ObservableProperty]
        private bool isMotorPollingEnabled;

        [ObservableProperty]
        private int motorPollingIntervalMs = 1000;

        public string MotionTotalTimeDisplay
            => SelectedDevice == null || SelectedDevice.MotionTotalTime == TimeSpan.Zero
                ? "Unavailable"
                : FormatTime(SelectedDevice.MotionTotalTime);

        public string MotionDataCountDisplay => SelectedDevice?.MotionDataCount.ToString() ?? "0";

        public string MotionCreatedAtDisplay
            => SelectedDevice == null || SelectedDevice.MotionCreatedAt == default
                ? "Unavailable"
                : SelectedDevice.MotionCreatedAt.ToString("yyyy-MM-dd HH:mm:ss");

        public DeviceDetailViewModel(ISerialService serialService)
        {
            _serialService = serialService;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        partial void OnSelectedDeviceChanged(Device? value)
        {
            CancelPendingLoads();
            StopMotorsPolling();
            StopStatusPolling();

            if (value != null)
            {
                ResetSnapshotState(value);
                FilesStatusMessage = "Loading files...";
                MotorsStatusMessage = "Loading motors...";
                LastLoadError = string.Empty;

                _deviceLoadCts = new CancellationTokenSource();
                _ = LoadDeviceSnapshotAsync(value, _deviceLoadCts.Token);
            }
            else
            {
                Files = new ObservableCollection<FileSystemItem>();
                SelectedFile = null;
                FileContent = string.Empty;
                FilesStatusMessage = string.Empty;
                MotorsStatusMessage = string.Empty;
                LastLoadError = string.Empty;
                NotifyOverviewBindings();
            }
        }

        partial void OnSelectedFileChanged(FileSystemItem? value)
        {
            if (value != null && !value.IsDirectory)
                _ = LoadFileContentAsync(value.Path);
            else
                FileContent = string.Empty;
        }

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

        public void SetMotorsPollingAllowed(bool allowed)
        {
            _isMotorsPollingAllowed = allowed;
            EnsureMotorsPollingState();
        }

        public void CancelPendingLoads()
        {
            try { _deviceLoadCts?.Cancel(); }
            catch { }
            finally
            {
                _deviceLoadCts?.Dispose();
                _deviceLoadCts = null;
                IsInitialLoadInProgress = false;
            }
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

        public void StopStatusPolling()
        {
            try { _statusPollingCts?.Cancel(); }
            catch { }
            finally
            {
                _statusPollingCts?.Dispose();
                _statusPollingCts = null;
                _statusPollingTask = null;
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
            if (SelectedDevice == null || !IsMotorPollingEnabled || !_isMotorsPollingAllowed || IsInitialLoadInProgress)
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
            var device = SelectedDevice;
            await RefreshMotorStateForDeviceAsync(device, token, updateStatusMessage: false);

            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(MotorPollingIntervalMs));
            while (await timer.WaitForNextTickAsync(token))
            {
                await RefreshMotorStateForDeviceAsync(device, token, updateStatusMessage: false);
            }
        }

        private void EnsureStatusPollingState(bool restartIfRunning = false)
        {
            if (!DeviceStatusRefreshPolicy.ShouldRun(SelectedDevice != null, IsInitialLoadInProgress))
            {
                StopStatusPolling();
                return;
            }

            if (_statusPollingTask != null && !_statusPollingTask.IsCompleted)
            {
                if (!restartIfRunning) return;
                StopStatusPolling();
            }

            _statusPollingCts = new CancellationTokenSource();
            _statusPollingTask = RunStatusPollingLoopAsync(_statusPollingCts.Token);
        }

        private async Task RunStatusPollingLoopAsync(CancellationToken token)
        {
            var device = SelectedDevice;
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(DeviceStatusRefreshPolicy.IntervalMs));
            while (await timer.WaitForNextTickAsync(token))
            {
                await RefreshDeviceStatusForDeviceAsync(device, token);
            }
        }

        [RelayCommand]
        private async Task RefreshMotorsAsync()
        {
            if (SelectedDevice == null) return;

            LastLoadError = string.Empty;
            await LoadMotorsForDeviceAsync(SelectedDevice, CancellationToken.None, announceRefresh: true);
            await RefreshMotorStateForDeviceAsync(SelectedDevice, CancellationToken.None, updateStatusMessage: true);
        }

        [RelayCommand]
        private async Task PlayMotionAsync()
        {
            if (SelectedDevice == null) return;
            var packet = BinarySerializer.EncodeMotionCtrl(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, BinaryMotionAction.Play);
            await _serialService.SendBinaryCommandAsync(SelectedDevice.Id, packet);
            await RefreshDeviceStatusForDeviceAsync(SelectedDevice, CancellationToken.None);
        }

        [RelayCommand]
        private async Task StopMotionAsync()
        {
            if (SelectedDevice == null) return;
            var packet = BinarySerializer.EncodeMotionCtrl(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, BinaryMotionAction.Stop);
            await _serialService.SendBinaryCommandAsync(SelectedDevice.Id, packet);
            await RefreshDeviceStatusForDeviceAsync(SelectedDevice, CancellationToken.None);
        }

        [RelayCommand]
        private async Task PauseMotionAsync()
        {
            if (SelectedDevice == null) return;
            var packet = BinarySerializer.EncodeMotionCtrl(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, BinaryMotionAction.Pause);
            await _serialService.SendBinaryCommandAsync(SelectedDevice.Id, packet);
            await RefreshDeviceStatusForDeviceAsync(SelectedDevice, CancellationToken.None);
        }

        [RelayCommand]
        private async Task SeekMotionAsync(double seconds)
        {
            if (SelectedDevice == null) return;
            var time = TimeSpan.FromSeconds(seconds);
            var packet = BinarySerializer.EncodeMotionCtrl(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, BinaryMotionAction.Seek, time.TotalSeconds);
            await _serialService.SendBinaryCommandAsync(SelectedDevice.Id, packet);
            await RefreshDeviceStatusForDeviceAsync(SelectedDevice, CancellationToken.None);
        }

        public string FormatTime(TimeSpan time) => time.ToString(@"hh\:mm\:ss");

        [RelayCommand]
        private async Task RefreshFilesAsync()
        {
            if (SelectedDevice == null) return;
            LastLoadError = string.Empty;
            await LoadFilesForDeviceAsync(SelectedDevice, CancellationToken.None, announceRefresh: true);
        }

        [RelayCommand]
        private async Task LoadFileContentAsync(string path)
        {
            if (SelectedDevice == null) return;

            IsFileLoading = true;
            try
            {
                var packet = BinarySerializer.EncodeGetFile(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, path);
                var responseBytes = await _serialService.SendBinaryQueryAsync(SelectedDevice.Id, BinaryCommand.GetFile, packet);
                if (!TryGetOkPayload(responseBytes, out _, out var payload, out var errorMessage))
                {
                    FilesStatusMessage = $"Failed to load file content: {errorMessage}";
                    RegisterLoadError("Files", errorMessage);
                    return;
                }

                var (_, content) = BinaryDeserializer.ParseGetFileResponse(payload);
                FileContent = content;
                FilesStatusMessage = $"Loaded file: {path}";
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

            LastLoadError = string.Empty;
            var validation = FirmwareFileRequestValidation.Validate(SelectedFile.Path, FileContent);
            if (!validation.IsValid)
            {
                FilesStatusMessage = $"Failed to save file: {validation.ErrorMessage}";
                RegisterLoadError("Files", validation.ErrorMessage);
                return;
            }

            var packet = BinarySerializer.EncodeSaveFile(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, SelectedFile.Path, FileContent);
            var responseBytes = await _serialService.SendBinaryQueryAsync(SelectedDevice.Id, BinaryCommand.SaveFile, packet);
            var result = SaveFileResponseProjection.Evaluate(responseBytes, SelectedFile.Path);

            FilesStatusMessage = result.StatusMessage;
            if (!result.Success)
                RegisterLoadError("Files", result.ErrorDetail);
        }

        [RelayCommand]
        private async Task VerifyFileAsync()
        {
            if (SelectedDevice == null || SelectedFile == null) return;

            LastLoadError = string.Empty;
            var validation = FirmwareFileRequestValidation.Validate(SelectedFile.Path, FileContent);
            if (!validation.IsValid)
            {
                var message = validation.ErrorMessage;
                FilesStatusMessage = $"Verification failed: {message}";
                RegisterLoadError("Files", message);
                VerificationResult = $"Verification failed: {message}";
                IsVerificationDialogOpen = true;
                return;
            }

            var packet = BinarySerializer.EncodeVerifyFile(BinaryProtocolConst.HostId, (byte)SelectedDevice.Id, SelectedFile.Path, FileContent);
            var responseBytes = await _serialService.SendBinaryQueryAsync(SelectedDevice.Id, BinaryCommand.VerifyFile, packet);
            if (!TryGetOkPayload(responseBytes, out _, out var payload, out var errorMessage))
            {
                FilesStatusMessage = $"Verification failed: {errorMessage}";
                RegisterLoadError("Files", errorMessage);
                VerificationResult = $"Verification failed: {errorMessage}";
                IsVerificationDialogOpen = true;
                return;
            }

            if (payload.Length < 3)
            {
                VerificationResult = "Verification failed: invalid device response.";
                IsVerificationDialogOpen = true;
                return;
            }

            string responsePath = BinaryDeserializer.ParseSaveFileResponse(payload[..^1]);
            if (!string.Equals(responsePath, SelectedFile.Path, StringComparison.Ordinal))
            {
                VerificationResult = "Verification failed: invalid device response.";
                IsVerificationDialogOpen = true;
                return;
            }

            bool match = BinaryDeserializer.ParseVerifyFileResponse(payload);

            VerificationResult = match ? "Content Matches Device" : "Content Mismatch";
            IsVerificationDialogOpen = true;
        }

        [RelayCommand]
        private void CloseVerificationDialog()
        {
            IsVerificationDialogOpen = false;
        }

        private async Task LoadDeviceSnapshotAsync(Device device, CancellationToken token)
        {
            IsInitialLoadInProgress = true;

            try
            {
                await RefreshDeviceStatusForDeviceAsync(device, token);
                token.ThrowIfCancellationRequested();

                await LoadFilesForDeviceAsync(device, token, announceRefresh: false);
                token.ThrowIfCancellationRequested();

                await LoadMotorsForDeviceAsync(device, token, announceRefresh: false);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                if (IsCurrentSelectedDevice(device))
                    IsInitialLoadInProgress = false;

                if (!token.IsCancellationRequested && IsCurrentSelectedDevice(device))
                {
                    EnsureMotorsPollingState();
                    EnsureStatusPollingState();
                }
            }
        }

        private async Task<bool> LoadFilesForDeviceAsync(Device? device, CancellationToken token, bool announceRefresh)
        {
            if (device == null || !IsCurrentSelectedDevice(device)) return false;

            IsFileLoading = true;
            if (announceRefresh)
                FilesStatusMessage = "Refreshing file list...";

            try
            {
                token.ThrowIfCancellationRequested();
                var responseBytes = await _serialService.SendBinaryQueryAsync(device.Id, BinaryCommand.GetFiles);
                token.ThrowIfCancellationRequested();

                if (!TryGetOkPayload(responseBytes, out _, out var payload, out var errorMessage))
                {
                    SetFilesFailure(errorMessage);
                    return false;
                }

                var entries = BinaryDeserializer.ParseGetFilesResponse(payload);
                var rootItems = BuildFileTreeFromFlatList(entries);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    if (!IsCurrentSelectedDevice(device)) return;

                    Files = new ObservableCollection<FileSystemItem>(rootItems);
                    SelectedFile = null;
                    FileContent = string.Empty;
                    ApplyMotionFileSummary(device, entries);
                    FilesStatusMessage = entries.Count == 0
                        ? "No files found."
                        : $"Loaded {entries.Count} file entries.";
                });

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetFilesFailure(ex.Message);
                return false;
            }
            finally
            {
                if (IsCurrentSelectedDevice(device))
                    IsFileLoading = false;
            }
        }

        private async Task<bool> LoadMotorsForDeviceAsync(Device? device, CancellationToken token, bool announceRefresh)
        {
            if (device == null || !IsCurrentSelectedDevice(device)) return false;

            if (announceRefresh)
                MotorsStatusMessage = "Refreshing motors...";

            try
            {
                token.ThrowIfCancellationRequested();
                var responseBytes = await _serialService.SendBinaryQueryAsync(device.Id, BinaryCommand.GetMotors);
                token.ThrowIfCancellationRequested();

                if (!TryGetOkPayload(responseBytes, out _, out var payload, out var errorMessage))
                {
                    SetMotorsFailure(errorMessage);
                    return false;
                }

                var patches = BinaryDeserializer.ParseGetMotorsResponse(payload);

                _dispatcherQueue.TryEnqueue(() =>
                {
                    if (!IsCurrentSelectedDevice(device)) return;

                    device.Motors.Clear();
                    MotorStateMerger.Apply(device.Motors, patches);
                    MotorsStatusMessage = patches.Count == 0
                        ? "No motors found."
                        : $"Loaded {patches.Count} motors.";
                });

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                SetMotorsFailure(ex.Message);
                return false;
            }
        }

        private async Task<bool> RefreshMotorStateForDeviceAsync(Device? device, CancellationToken token, bool updateStatusMessage)
        {
            if (device == null || !IsCurrentSelectedDevice(device)) return false;

            try
            {
                token.ThrowIfCancellationRequested();
                var responseBytes = await _serialService.SendBinaryQueryAsync(device.Id, BinaryCommand.GetMotorState);
                token.ThrowIfCancellationRequested();

                if (!TryGetOkPayload(responseBytes, out _, out var payload, out var errorMessage))
                {
                    if (updateStatusMessage)
                        SetMotorsFailure(errorMessage);
                    return false;
                }

                var patches = BinaryDeserializer.ParseMotorStateResponse(payload);
                _dispatcherQueue.TryEnqueue(() =>
                {
                    if (!IsCurrentSelectedDevice(device)) return;

                    MotorStateMerger.Apply(device.Motors, patches);
                    if (updateStatusMessage)
                    {
                        MotorsStatusMessage = patches.Count == 0
                            ? "No motor state returned."
                            : $"Updated {patches.Count} motor states.";
                    }
                });

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (updateStatusMessage)
                    SetMotorsFailure(ex.Message);
                return false;
            }
        }

        private async Task<bool> RefreshDeviceStatusForDeviceAsync(Device? device, CancellationToken token)
        {
            if (device == null || !IsCurrentSelectedDevice(device)) return false;

            try
            {
                token.ThrowIfCancellationRequested();
                var refreshedDevice = await _serialService.PingDeviceAsync(device.Id);
                token.ThrowIfCancellationRequested();

                if (refreshedDevice == null)
                    return false;

                _dispatcherQueue.TryEnqueue(() =>
                {
                    if (!IsCurrentSelectedDevice(device)) return;

                    ApplyDeviceStatus(device, refreshedDevice);
                    NotifyOverviewBindings();
                });

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyDeviceStatus(Device target, Device source)
        {
            target.IsConnected = source.IsConnected;
            target.StatusMessage = source.StatusMessage;
            target.MotionState = source.MotionState;
            target.MotionCurrentTime = source.MotionCurrentTime;
            target.MotionTotalTime = source.MotionTotalTime;
            target.Address64 = source.Address64;
        }

        private void ResetSnapshotState(Device device)
        {
            Files = new ObservableCollection<FileSystemItem>();
            SelectedFile = null;
            FileContent = string.Empty;
            device.Motors.Clear();
            device.MotionTotalTime = TimeSpan.Zero;
            device.MotionDataCount = 0;
            device.MotionCreatedAt = default;
            NotifyOverviewBindings();
        }

        private void ApplyMotionFileSummary(Device device, List<BinaryDeserializer.FileEntry> entries)
        {
            device.MotionDataCount = MotionFileSummary.CountMotionDataFiles(entries.Where(entry => !entry.IsDirectory).Select(entry => entry.Path));
            device.MotionTotalTime = TimeSpan.Zero;
            device.MotionCreatedAt = default;
            NotifyOverviewBindings();
        }

        private bool IsCurrentSelectedDevice(Device? device)
            => device != null && ReferenceEquals(SelectedDevice, device);

        private void SetFilesFailure(string message)
        {
            var text = $"Failed to load files: {message}";
            _dispatcherQueue.TryEnqueue(() =>
            {
                FilesStatusMessage = text;
                RegisterLoadError("Files", message);
            });
        }

        private void SetMotorsFailure(string message)
        {
            var text = $"Failed to load motors: {message}";
            _dispatcherQueue.TryEnqueue(() =>
            {
                MotorsStatusMessage = text;
                RegisterLoadError("Motors", message);
            });
        }

        private void RegisterLoadError(string section, string message)
        {
            var formatted = $"{section}: {message}";
            LastLoadError = string.IsNullOrWhiteSpace(LastLoadError)
                ? formatted
                : $"{LastLoadError}\n{formatted}";
        }

        private void NotifyOverviewBindings()
        {
            OnPropertyChanged(nameof(MotionTotalTimeDisplay));
            OnPropertyChanged(nameof(MotionDataCountDisplay));
            OnPropertyChanged(nameof(MotionCreatedAtDisplay));
        }

        private static bool TryGetOkPayload(byte[]? responseBytes, out ResponseHeader header, out byte[] payload, out string errorMessage)
        {
            header = default;
            payload = Array.Empty<byte>();

            if (responseBytes == null)
            {
                errorMessage = "No response from device.";
                return false;
            }

            if (!BinaryDeserializer.TryParseResponseHeader(responseBytes, out header))
            {
                errorMessage = "Invalid response header.";
                return false;
            }

            int payloadStart = BinaryProtocolConst.ResponseHeaderSize;
            if (responseBytes.Length < payloadStart + header.PayloadLen)
            {
                errorMessage = "Incomplete response payload.";
                return false;
            }

            payload = responseBytes[payloadStart..(payloadStart + header.PayloadLen)];
            if (BinaryDeserializer.IsOk(header))
            {
                errorMessage = string.Empty;
                return true;
            }

            if (payload.Length > 0)
            {
                var (code, message) = BinaryDeserializer.ParseErrorResponse(payload);
                errorMessage = string.IsNullOrWhiteSpace(message)
                    ? BinaryProtocolErrorText.Describe(code, header.Cmd)
                    : message;
            }
            else
            {
                errorMessage = $"Device returned {header.Status} for {header.Cmd}.";
            }

            return false;
        }

        private static List<FileSystemItem> BuildFileTreeFromFlatList(List<BinaryDeserializer.FileEntry> entries)
        {
            var nodes = entries.Select(e => new FileSystemItem
            {
                Name = e.Name,
                Path = e.Path,
                IsDirectory = e.IsDirectory,
                Size = e.Size,
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
