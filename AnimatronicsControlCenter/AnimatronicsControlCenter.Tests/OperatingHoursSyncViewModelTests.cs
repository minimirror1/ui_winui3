using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;
using AnimatronicsControlCenter.Infrastructure;
using AnimatronicsControlCenter.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class OperatingHoursSyncViewModelTests
{
    // ─────────────────────────────── Constructor ────────────────────────────────

    [TestMethod]
    public void Constructor_InitializesDeviceRangeAndStoreIdFromSettings()
    {
        var settings = TestSettings();
        settings.ScanStartId   = 4;
        settings.ScanEndId     = 6;
        settings.BackendStoreId = "store-from-settings";

        var vm = BuildVm(settings: settings);

        Assert.AreEqual(4, vm.DeviceRangeFrom);
        Assert.AreEqual(6, vm.DeviceRangeTo);
        Assert.AreEqual("store-from-settings", vm.StoreId);
        Assert.AreEqual(7, vm.ServerDays.Count);
        Assert.AreEqual(7, vm.DeviceDays.Count);
    }

    // ──────────────────────────── LoadFromServer ─────────────────────────────────

    [TestMethod]
    public async Task LoadFromServerCommand_PopulatesServerDaysFromSchedule()
    {
        var schedule = TestSchedule();
        var vm = BuildVm(source: new FakeSource { Result = Ok(schedule) });

        await vm.LoadFromServerCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.IsServerScheduleLoaded);
        Assert.AreEqual(7, vm.ServerDays.Count);
        Assert.AreEqual("월", vm.ServerDays[0].DayLabel);
        Assert.AreEqual("MON", vm.ServerDays[0].DayKey);
        Assert.AreEqual(TimeSpan.FromMinutes(540), vm.ServerDays[0].OpenTime);
        Assert.AreEqual(TimeSpan.FromMinutes(1080), vm.ServerDays[0].CloseTime);
        Assert.IsFalse(vm.ServerDays[0].IsClosed);
        Assert.IsTrue(vm.ServerDays[5].IsClosed);   // SAT closed
        Assert.IsTrue(vm.ServerDays[6].IsClosed);   // SUN closed
    }

    [TestMethod]
    public async Task LoadFromServerCommand_SetsStoreIdAndInfoText()
    {
        var schedule = TestSchedule();
        var vm = BuildVm(source: new FakeSource { Result = Ok(schedule) });

        await vm.LoadFromServerCommand.ExecuteAsync(null);

        Assert.AreEqual("store-1", vm.StoreId);
        StringAssert.Contains(vm.StoreInfoText, "Seoul Store");
        StringAssert.Contains(vm.StoreInfoText, "Asia/Seoul");
    }

    [TestMethod]
    public async Task LoadFromServerCommand_SetsStatusMessageOnFailure()
    {
        var vm = BuildVm(source: new FakeSource
        {
            Result = new OperatingHoursSourceResult(false, false, "서버 오류", null),
        });

        await vm.LoadFromServerCommand.ExecuteAsync(null);

        Assert.IsFalse(vm.IsServerScheduleLoaded);
        StringAssert.Contains(vm.StatusMessage, "서버 오류");
    }

    // ─────────────────────────── PushToServer ────────────────────────────────────

    // ─────────────────────────── SendToAllDevices ────────────────────────────────

    [TestMethod]
    public async Task PushToServerCommand_CallsSourceSaveWithEditedServerDays()
    {
        var source = new FakeSource { SaveResult = Ok(TestSchedule()) };
        var vm = BuildVm(source: source);
        vm.ServerDays[0].OpenTime = TimeSpan.FromHours(10);
        vm.ServerDays[0].CloseTime = TimeSpan.FromHours(19);

        await vm.PushToServerCommand.ExecuteAsync(null);

        Assert.IsNotNull(source.SavedSchedule);
        Assert.AreEqual("MON", source.SavedSchedule!.Days[0].DayOfWeek);
        Assert.AreEqual((ushort)600, source.SavedSchedule.Days[0].OpenMinutes);
        Assert.AreEqual((ushort)1140, source.SavedSchedule.Days[0].CloseMinutes);
    }

    [TestMethod]
    public async Task PushToServerCommand_RefreshesServerDaysOnSuccess()
    {
        var source = new FakeSource { SaveResult = Ok(TestSchedule()) };
        var vm = BuildVm(source: source);
        vm.ServerDays[0].OpenTime = TimeSpan.FromHours(10);

        await vm.PushToServerCommand.ExecuteAsync(null);

        Assert.IsTrue(vm.IsServerScheduleLoaded);
        Assert.AreEqual("store-1", vm.StoreId);
        Assert.IsFalse(vm.ServerDays.Any(d => d.HasChanged));
        StringAssert.Contains(vm.StatusMessage, "OK");
    }

    [TestMethod]
    public async Task PushToServerCommand_ShowsFailureMessage()
    {
        var source = new FakeSource
        {
            SaveResult = new OperatingHoursSourceResult(false, false, "Save failed", null),
        };
        var vm = BuildVm(source: source);

        await vm.PushToServerCommand.ExecuteAsync(null);

        StringAssert.Contains(vm.StatusMessage, "Save failed");
    }

    [TestMethod]
    public async Task SendToAllDevicesCommand_CallsSyncServiceWithCorrectRange()
    {
        var schedule = TestSchedule();
        var sync     = new FakeSyncService();
        var vm = BuildVm(
            source: new FakeSource { Result = Ok(schedule) },
            sync:   sync);
        vm.DeviceRangeFrom = 3;
        vm.DeviceRangeTo   = 5;
        await vm.LoadFromServerCommand.ExecuteAsync(null);

        await vm.SendToAllDevicesCommand.ExecuteAsync(null);

        Assert.AreEqual(3, sync.StartDeviceId);
        Assert.AreEqual(5, sync.EndDeviceId);
        StringAssert.Contains(vm.StatusMessage, "일괄 전송 완료");
    }

    // ─────────────────────────── Compare ─────────────────────────────────────────

    [TestMethod]
    public async Task CompareCurrentDeviceCommand_BuildsMatchResult_WhenScheduleMatches()
    {
        var schedule = TestSchedule();
        var serial   = new FakeSerialService
        {
            ReadResult = ReadOk(1, schedule.Days),
        };
        var vm = BuildVm(
            source: new FakeSource { Result = Ok(schedule) },
            serial: serial);
        await vm.LoadFromServerCommand.ExecuteAsync(null);
        vm.CurrentDeviceId = 1;

        bool eventFired = false;
        vm.CompareRequested += (_, _) => eventFired = true;
        await vm.CompareCurrentDeviceCommand.ExecuteAsync(null);

        Assert.AreEqual(1, vm.CompareResults.Count);
        Assert.AreEqual(DeviceCompareStatus.Match, vm.CompareResults[0].Status);
        Assert.IsTrue(eventFired);
    }

    [TestMethod]
    public async Task CompareCurrentDeviceCommand_BuildsDiffResult_WhenTimesDiffer()
    {
        var schedule = TestSchedule();
        // device has Friday close at 22:00 instead of 23:00
        var deviceDays = schedule.Days.Select((d, i) =>
            d.DayOfWeek == "FRI" ? new OperatingHoursDay("FRI", 540, 1320) : d).ToArray();
        var serial = new FakeSerialService { ReadResult = ReadOk(1, deviceDays) };
        var vm = BuildVm(
            source: new FakeSource { Result = Ok(schedule) },
            serial: serial);
        await vm.LoadFromServerCommand.ExecuteAsync(null);
        vm.CurrentDeviceId = 1;

        await vm.CompareCurrentDeviceCommand.ExecuteAsync(null);

        Assert.AreEqual(DeviceCompareStatus.Diff, vm.CompareResults[0].Status);
        Assert.IsTrue(vm.CompareResults[0].DiffDayCount > 0);
    }

    [TestMethod]
    public async Task CompareCurrentDeviceCommand_BuildsErrorResult_WhenDeviceNoResponse()
    {
        var schedule = TestSchedule();
        var serial   = new FakeSerialService { ReadResult = null };
        var vm = BuildVm(
            source: new FakeSource { Result = Ok(schedule) },
            serial: serial);
        await vm.LoadFromServerCommand.ExecuteAsync(null);
        vm.CurrentDeviceId = 1;

        await vm.CompareCurrentDeviceCommand.ExecuteAsync(null);

        Assert.AreEqual(DeviceCompareStatus.Error, vm.CompareResults[0].Status);
    }

    [TestMethod]
    public async Task CompareAllDevicesCommand_BuildsResultForEachDevice()
    {
        var schedule = TestSchedule();
        var vm = BuildVm(
            source: new FakeSource { Result = Ok(schedule) },
            serial: new FakeSerialService { ReadResult = ReadOk(1, schedule.Days) });
        vm.DeviceRangeFrom = 1;
        vm.DeviceRangeTo   = 3;
        await vm.LoadFromServerCommand.ExecuteAsync(null);

        bool eventFired = false;
        vm.CompareRequested += (_, _) => eventFired = true;
        await vm.CompareAllDevicesCommand.ExecuteAsync(null);

        Assert.AreEqual(3, vm.CompareResults.Count);
        Assert.IsTrue(eventFired);
        Assert.AreEqual(3, vm.CompareMatchCount);
    }

    // ─────────────────────────── Navigation ──────────────────────────────────────

    [TestMethod]
    public void NavigatePrevCommand_CanNotExecute_AtRangeStart()
    {
        var vm = BuildVm();
        vm.DeviceRangeFrom = 1;
        vm.DeviceRangeTo   = 5;
        vm.CurrentDeviceId = 1;

        Assert.IsFalse(vm.NavigatePrevCommand.CanExecute(null));
    }

    [TestMethod]
    public void NavigateNextCommand_IncreasesId_WithinRange()
    {
        var vm = BuildVm();
        vm.DeviceRangeFrom = 1;
        vm.DeviceRangeTo   = 5;
        vm.CurrentDeviceId = 3;

        vm.NavigateNextCommand.Execute(null);

        Assert.AreEqual(4, vm.CurrentDeviceId);
    }

    // ─────────────────────────── Helpers ─────────────────────────────────────────

    private static OperatingHoursSyncViewModel BuildVm(
        SettingsService?              settings = null,
        FakeSource?                   source   = null,
        FakeSyncService?              sync     = null,
        FakeSerialService?            serial   = null)
        => new(
            settings ?? TestSettings(),
            source   ?? new FakeSource(),
            sync     ?? new FakeSyncService(),
            serial   ?? new FakeSerialService());

    private static SettingsService TestSettings()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "ui_winui3_ops_sync_vm_tests",
            Guid.NewGuid().ToString("N"),
            "backend-settings.json");
        return new SettingsService(new FakeBackendSettingsPathProvider(path));
    }

    private static OperatingHoursSchedule TestSchedule(string timezone = "Asia/Seoul")
    {
        var days = new[]
        {
            new OperatingHoursDay("MON", 540, 1080),
            new OperatingHoursDay("TUE", 540, 1080),
            new OperatingHoursDay("WED", 540, 1080),
            new OperatingHoursDay("THU", 540, 1080),
            new OperatingHoursDay("FRI", 540, 1380),  // close 23:00
            new OperatingHoursDay("SAT", 0, 0),
            new OperatingHoursDay("SUN", 0, 0),
        };
        return new OperatingHoursSchedule("store-1", "Seoul Store", timezone, "2026-02-26T01:49:43.727Z", days, 1234);
    }

    private static OperatingHoursSourceResult Ok(OperatingHoursSchedule schedule)
        => new(true, false, "OK", schedule);

    private static OperatingHoursDeviceReadResult ReadOk(int id, IReadOnlyList<OperatingHoursDay> days)
    {
        var deviceSchedule = new OperatingHoursDeviceSchedule(540, 0, days);
        return new OperatingHoursDeviceReadResult(id, true, deviceSchedule, "OK");
    }

    // ─────────────────────────── Fakes ──────────────────────────────────────────

    private sealed class FakeSource : IOperatingHoursSource
    {
        public OperatingHoursSourceResult? Result { get; set; }
        public OperatingHoursSourceResult? SaveResult { get; set; }
        public OperatingHoursSchedule? SavedSchedule { get; private set; }

        public Task<OperatingHoursSourceResult> LoadAsync(CancellationToken cancellationToken)
            => Task.FromResult(Result ?? new OperatingHoursSourceResult(false, false, "No schedule", null));

        public Task<OperatingHoursSourceResult> SaveAsync(OperatingHoursSchedule schedule, CancellationToken cancellationToken)
        {
            SavedSchedule = schedule;
            return Task.FromResult(SaveResult ?? new OperatingHoursSourceResult(false, false, "Save failed", null));
        }
    }

    private sealed class FakeSyncService : IOperatingHoursDeviceSyncService
    {
        public int StartDeviceId { get; private set; }
        public int EndDeviceId   { get; private set; }

        public Task<IReadOnlyList<OperatingHoursDeviceWriteResult>> SyncRangeAsync(
            int startDeviceId, int endDeviceId, OperatingHoursSchedule schedule,
            CancellationToken cancellationToken)
        {
            StartDeviceId = startDeviceId;
            EndDeviceId   = endDeviceId;
            return Task.FromResult<IReadOnlyList<OperatingHoursDeviceWriteResult>>(
                Enumerable.Range(startDeviceId, endDeviceId - startDeviceId + 1)
                    .Select(id => new OperatingHoursDeviceWriteResult(id, true, schedule.Checksum, "OK"))
                    .ToArray());
        }
    }

    private sealed class FakeSerialService : ISerialService
    {
        public OperatingHoursDeviceReadResult? ReadResult { get; set; }
        public bool IsConnected => true;
        public Task ConnectAsync(string portName, int baudRate) => Task.CompletedTask;
        public void Disconnect() { }
        public Task SendBinaryCommandAsync(int deviceId, byte[] packet) => Task.CompletedTask;
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, AnimatronicsControlCenter.Core.Protocol.BinaryCommand cmd) => Task.FromResult<byte[]?>(null);
        public Task<byte[]?> SendBinaryQueryAsync(int deviceId, AnimatronicsControlCenter.Core.Protocol.BinaryCommand cmd, byte[] packet) => Task.FromResult<byte[]?>(null);
        public Task<AnimatronicsControlCenter.Core.Models.Device?> PingDeviceAsync(int deviceId) => Task.FromResult<AnimatronicsControlCenter.Core.Models.Device?>(null);
        public Task<IEnumerable<AnimatronicsControlCenter.Core.Models.Device>> ScanDevicesAsync(int startId, int endId) => Task.FromResult(Enumerable.Empty<AnimatronicsControlCenter.Core.Models.Device>());
        public Task<OperatingHoursDeviceWriteResult> SetOperatingHoursAsync(int deviceId, OperatingHoursSchedule schedule)
            => Task.FromResult(new OperatingHoursDeviceWriteResult(deviceId, true, schedule.Checksum, "OK"));
        public Task<OperatingHoursDeviceReadResult> GetOperatingHoursAsync(int deviceId)
            => Task.FromResult(ReadResult ?? new OperatingHoursDeviceReadResult(deviceId, false, null, "No result"));
    }

    private sealed class FakeBackendSettingsPathProvider : IBackendSettingsPathProvider
    {
        public FakeBackendSettingsPathProvider(string filePath) => BackendSettingsFilePath = filePath;
        public string BackendSettingsFilePath { get; }
    }
}
