using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.Utilities;

namespace AnimatronicsControlCenter.Infrastructure
{
    /// SNTP(UDP 123) 기반 인터넷 시간 동기화 서비스.
    /// 서버 목록을 순차 시도하고, 성공하면 보정값(서버 UTC − PC UTC)만 저장한다.
    /// 모든 서버가 실패하면 마지막 보정값(없으면 0 = PC 시계)을 유지한다.
    public class NetworkTimeService : INetworkTimeService, IDisposable
    {
        public static readonly IReadOnlyList<string> DefaultServers =
            ["time.windows.com", "pool.ntp.org", "time.google.com"];

        private const int NtpPort = 123;
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);

        private readonly IReadOnlyList<string> _servers;
        private readonly Func<string, byte[], CancellationToken, Task<byte[]>> _transport;
        private readonly Func<DateTimeOffset> _clock;
        private readonly object _gate = new();
        private TimeSpan _correction = TimeSpan.Zero;
        private TimeSyncStatus _status = new(false, null, null, TimeSpan.Zero, null);
        private Timer? _timer;

        public NetworkTimeService()
            : this(DefaultServers, SendSntpRequestAsync, () => DateTimeOffset.UtcNow)
        {
        }

        public NetworkTimeService(
            IReadOnlyList<string> servers,
            Func<string, byte[], CancellationToken, Task<byte[]>> transport,
            Func<DateTimeOffset> clock)
        {
            _servers = servers;
            _transport = transport;
            _clock = clock;
        }

        public DateTimeOffset UtcNow
        {
            get
            {
                lock (_gate)
                {
                    return _clock() + _correction;
                }
            }
        }

        public TimeSyncStatus Status
        {
            get
            {
                lock (_gate)
                {
                    return _status;
                }
            }
        }

        public event EventHandler<TimeSyncStatus>? StatusChanged;

        public async Task<bool> SynchronizeAsync(CancellationToken cancellationToken = default)
        {
            string? lastError = null;

            foreach (var server in _servers)
            {
                try
                {
                    byte[] request = SntpPacket.BuildRequest();
                    var requestSentUtc = _clock();
                    byte[] response = await _transport(server, request, cancellationToken).ConfigureAwait(false);
                    var responseReceivedUtc = _clock();

                    if (!SntpPacket.TryParseCorrection(response, requestSentUtc, responseReceivedUtc,
                            out TimeSpan correction, out string? parseError))
                    {
                        lastError = $"{server}: {parseError}";
                        continue;
                    }

                    TimeSyncStatus status;
                    lock (_gate)
                    {
                        _correction = correction;
                        _status = new TimeSyncStatus(true, server, _clock() + correction, correction, null);
                        status = _status;
                    }

                    StatusChanged?.Invoke(this, status);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastError = $"{server}: {ex.Message}";
                }
            }

            TimeSyncStatus failedStatus;
            lock (_gate)
            {
                // 이전 성공 보정값은 유지한 채 오류만 기록한다.
                _status = _status with { LastError = lastError };
                failedStatus = _status;
            }

            StatusChanged?.Invoke(this, failedStatus);
            return false;
        }

        public void Start(TimeSpan syncInterval)
        {
            lock (_gate)
            {
                _timer ??= new Timer(_ => _ = SynchronizeSafeAsync(), null, TimeSpan.Zero, syncInterval);
            }
        }

        private async Task SynchronizeSafeAsync()
        {
            try
            {
                await SynchronizeAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        }

        private static async Task<byte[]> SendSntpRequestAsync(string server, byte[] request, CancellationToken cancellationToken)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(RequestTimeout);

            using var udp = new UdpClient();
            udp.Connect(server, NtpPort);
            await udp.SendAsync(request.AsMemory(), timeoutCts.Token).ConfigureAwait(false);
            UdpReceiveResult result = await udp.ReceiveAsync(timeoutCts.Token).ConfigureAwait(false);
            return result.Buffer;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
