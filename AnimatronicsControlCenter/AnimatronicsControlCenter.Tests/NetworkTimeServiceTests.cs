using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Utilities;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class NetworkTimeServiceTests
{
    private static readonly DateTimeOffset BaseUtc = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void UtcNow_BeforeAnySync_ReturnsPcClock()
    {
        var service = new NetworkTimeService(["ntp.example"], FailingTransport, () => BaseUtc);

        Assert.AreEqual(BaseUtc, service.UtcNow);
        Assert.IsFalse(service.Status.IsSynchronized);
    }

    [TestMethod]
    public async Task SynchronizeAsync_ServerAheadTenSeconds_AppliesCorrectionToUtcNow()
    {
        var transport = MakeTransport(serverOffset: TimeSpan.FromSeconds(10));
        var service = new NetworkTimeService(["ntp.example"], transport, () => BaseUtc);

        bool ok = await service.SynchronizeAsync();

        Assert.IsTrue(ok);
        Assert.IsTrue(service.Status.IsSynchronized);
        Assert.AreEqual("ntp.example", service.Status.SyncedServer);
        var drift = (service.UtcNow - BaseUtc.AddSeconds(10)).Duration();
        Assert.IsTrue(drift < TimeSpan.FromMilliseconds(50), $"drift={drift.TotalMilliseconds}ms");
    }

    [TestMethod]
    public async Task SynchronizeAsync_FirstServerFails_FallsBackToSecond()
    {
        int calls = 0;
        Task<byte[]> Transport(string server, byte[] request, CancellationToken ct)
        {
            calls++;
            if (server == "bad.example") throw new TimeoutException("no response");
            return MakeTransport(TimeSpan.Zero)(server, request, ct);
        }

        var service = new NetworkTimeService(["bad.example", "good.example"], Transport, () => BaseUtc);

        bool ok = await service.SynchronizeAsync();

        Assert.IsTrue(ok);
        Assert.AreEqual(2, calls);
        Assert.AreEqual("good.example", service.Status.SyncedServer);
    }

    [TestMethod]
    public async Task SynchronizeAsync_AllServersFail_KeepsPcClockAndReportsError()
    {
        var service = new NetworkTimeService(["a.example", "b.example"], FailingTransport, () => BaseUtc);

        bool ok = await service.SynchronizeAsync();

        Assert.IsFalse(ok);
        Assert.IsFalse(service.Status.IsSynchronized);
        Assert.IsNotNull(service.Status.LastError);
        Assert.AreEqual(BaseUtc, service.UtcNow);
    }

    [TestMethod]
    public async Task SynchronizeAsync_FailureAfterSuccess_KeepsLastCorrection()
    {
        bool fail = false;
        Task<byte[]> Transport(string server, byte[] request, CancellationToken ct)
        {
            if (fail) throw new TimeoutException("offline");
            return MakeTransport(TimeSpan.FromSeconds(10))(server, request, ct);
        }

        var service = new NetworkTimeService(["ntp.example"], Transport, () => BaseUtc);
        await service.SynchronizeAsync();

        fail = true;
        bool ok = await service.SynchronizeAsync();

        Assert.IsFalse(ok);
        Assert.IsTrue(service.Status.IsSynchronized, "이전 성공 보정값은 유지되어야 한다.");
        var drift = (service.UtcNow - BaseUtc.AddSeconds(10)).Duration();
        Assert.IsTrue(drift < TimeSpan.FromMilliseconds(50));
    }

    [TestMethod]
    public async Task SynchronizeAsync_RaisesStatusChanged()
    {
        var statuses = new List<TimeSyncStatus>();
        var service = new NetworkTimeService(["ntp.example"], MakeTransport(TimeSpan.Zero), () => BaseUtc);
        service.StatusChanged += (_, status) => statuses.Add(status);

        await service.SynchronizeAsync();

        Assert.AreEqual(1, statuses.Count);
        Assert.IsTrue(statuses[0].IsSynchronized);
    }

    [TestMethod]
    public async Task Start_TriggersInitialSynchronization()
    {
        int calls = 0;
        var firstCall = new TaskCompletionSource();
        Task<byte[]> Transport(string server, byte[] request, CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            firstCall.TrySetResult();
            return MakeTransport(TimeSpan.Zero)(server, request, ct);
        }

        using var service = new NetworkTimeService(["ntp.example"], Transport, () => BaseUtc);
        service.Start(TimeSpan.FromHours(1));

        await Task.WhenAny(firstCall.Task, Task.Delay(2000));
        Assert.IsTrue(calls >= 1, "Start()는 즉시 1회 동기화를 트리거해야 한다.");
    }

    /// 서버 시계가 serverOffset만큼 앞선 SNTP 응답을 흉내낸다.
    /// 주입된 clock(BaseUtc)과 같은 기준을 쓰므로 correction == serverOffset이 된다.
    private static Func<string, byte[], CancellationToken, Task<byte[]>> MakeTransport(TimeSpan serverOffset)
    {
        return (server, request, ct) =>
        {
            var serverNow = BaseUtc + serverOffset;
            var response = new byte[48];
            response[0] = 0x24; // server mode
            response[1] = 2;    // stratum 2
            SntpPacket.WriteTimestamp(response, 32, serverNow);
            SntpPacket.WriteTimestamp(response, 40, serverNow);
            return Task.FromResult(response);
        };
    }

    private static Task<byte[]> FailingTransport(string server, byte[] request, CancellationToken ct)
        => throw new TimeoutException("unreachable");
}
