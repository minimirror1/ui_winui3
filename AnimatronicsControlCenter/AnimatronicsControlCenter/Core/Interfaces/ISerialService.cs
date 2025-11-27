using System.Collections.Generic;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Interfaces
{
    public interface ISerialService
    {
        bool IsConnected { get; }
        Task ConnectAsync(string portName, int baudRate);
        void Disconnect();
        Task SendCommandAsync(int deviceId, string command, object? payload = null);
        Task<string?> SendQueryAsync(int deviceId, string command, object? payload = null);
        Task<Device?> PingDeviceAsync(int deviceId);
        Task<IEnumerable<Device>> ScanDevicesAsync(int startId, int endId);
    }
}
