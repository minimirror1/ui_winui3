using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AnimatronicsControlCenter.Infrastructure
{
    internal sealed class DeviceCommandGate
    {
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _gates = new();

        public Task RunExclusiveAsync(int deviceId, Func<Task> action)
            => RunExclusiveAsync(deviceId, async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            });

        public async Task<T> RunExclusiveAsync<T>(int deviceId, Func<Task<T>> action)
        {
            var gate = _gates.GetOrAdd(deviceId, static _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync().ConfigureAwait(false);
            try
            {
                return await action().ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
