using System.Collections.Generic;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Protocol;

namespace AnimatronicsControlCenter.Core.Interfaces
{
    public interface ISerialService
    {
        bool IsConnected { get; }
        Task ConnectAsync(string portName, int baudRate);
        void Disconnect();

        // Binary 전송 (Fire-and-forget)
        Task SendBinaryCommandAsync(int deviceId, byte[] packet);

        // Binary 쿼리 (응답 대기) — 전체 응답 패킷(헤더+페이로드) 반환
        // payload 없는 요청 (ping, get_motors, get_motor_state, get_files)
        Task<byte[]?> SendBinaryQueryAsync(int deviceId, BinaryCommand cmd);

        // payload 있는 요청 (get_file, verify_file): 호출자가 BinarySerializer로 packet 생성
        Task<byte[]?> SendBinaryQueryAsync(int deviceId, BinaryCommand cmd, byte[] packet);

        Task<Device?> PingDeviceAsync(int deviceId);
        Task<IEnumerable<Device>> ScanDevicesAsync(int startId, int endId);
    }
}
