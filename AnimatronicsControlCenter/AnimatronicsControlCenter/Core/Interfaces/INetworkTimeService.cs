using System;
using System.Threading;
using System.Threading.Tasks;

namespace AnimatronicsControlCenter.Core.Interfaces
{
    /// 시간 동기화 상태 스냅샷.
    /// IsSynchronized가 false면 UtcNow는 PC 시계를 그대로 반환한다(폴백).
    public sealed record TimeSyncStatus(
        bool IsSynchronized,
        string? SyncedServer,
        DateTimeOffset? LastSyncUtc,
        TimeSpan CorrectionOffset,
        string? LastError);

    /// 신뢰할 수 있는 인터넷 시간 서버(SNTP) 기준의 현재 시각 제공자.
    /// PC 시계를 바꾸지 않고 보정값만 유지한다.
    public interface INetworkTimeService
    {
        /// 보정 적용된 현재 UTC. 동기화 전/실패 시에는 PC 시계 값.
        DateTimeOffset UtcNow { get; }

        TimeSyncStatus Status { get; }

        event EventHandler<TimeSyncStatus>? StatusChanged;

        Task<bool> SynchronizeAsync(CancellationToken cancellationToken = default);

        /// 즉시 1회 동기화 후 주기 재동기화를 시작한다.
        void Start(TimeSpan syncInterval);
    }
}
