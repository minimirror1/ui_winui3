using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Protocol;

namespace AnimatronicsControlCenter.Infrastructure
{
    public class VirtualDeviceManager
    {
        // Store file systems per device ID
        private readonly Dictionary<int, Dictionary<string, string>> _deviceFileSystems;
        private readonly Dictionary<int, List<MotorState>> _deviceMotors = new();
        private readonly Dictionary<int, int> _deviceMotorTick = new();
        private readonly Dictionary<int, PongStatus> _devicePingStatuses = new();

        public VirtualDeviceManager()
        {
            _deviceFileSystems = new Dictionary<int, Dictionary<string, string>>();
        }

        // ── 파일시스템 초기화 ───────────────────────────────────────────

        private Dictionary<string, string> GetDefaultFileSystem(int deviceId)
        {
            return new Dictionary<string, string>
            {
                { "Error/err_lv.ini", "[ErrorLevel]\nLevel=1" },
                { "Error/ERR_LVF.TXT", "2023-10-27 10:00:00 ERROR_01" },
                { "Error/note.ini", "Note=Check sensors" },

                { "Log/BOOT.TXT", "Boot Log..." },
                { "Log/ERROR.TXT", "Error Log..." },
                { "Log/INSP.TXT", "Inspection Log..." },
                { "Log/SENSOR.TXT", "Sensor Log..." },

                { "Media/MT_2.CSV", "Time,Pos\n0,10\n100,20" },
                { "Media/MT_3.CSV", "Time,Pos\n0,10\n100,20" },
                { "Media/MT_4.CSV", "Time,Pos\n0,10\n100,20" },
                { "Media/MT_5.CSV", "Time,Pos\n0,10\n100,20" },
                { "Media/MT_6.CSV", "Time,Pos\n0,10\n100,20" },
                { "Media/MT_ALL.CSV", "Time,Pos\n0,10\n100,20" },

                { "Midi/motor/placeholder.txt", "" },
                { "Midi/page/placeholder.txt", "" },

                { "Setting/DI_ID.TXT", $"DeviceID={deviceId}" },
                { "Setting/MT_AT.TXT", "Value=100" },
                { "Setting/MT_ATT.TXT", "Value=200" },
                { "Setting/MT_CT.TXT", "Value=300" },
                { "Setting/MT_EL.TXT", "Value=400" },
                { "Setting/MT_LI.TXT", "Value=500" },
                { "Setting/MT_LK.TXT", "Value=600" },
                { "Setting/MT_MD.TXT", "Value=700" },
                { "Setting/MT_MS.TXT", "Value=800" },
                { "Setting/MT_PL.TXT", "Value=900" },
                { "Setting/MT_RP.TXT", "Value=1000" },
                { "Setting/MT_ST.TXT", "Value=1100" },
                { "Setting/RE_TI.TXT", "Value=1200" }
            };
        }

        private Dictionary<string, string> GetFileSystem(int deviceId)
        {
            if (!_deviceFileSystems.ContainsKey(deviceId))
                _deviceFileSystems[deviceId] = GetDefaultFileSystem(deviceId);
            return _deviceFileSystems[deviceId];
        }

        private List<MotorState> GetMotors(int deviceId)
        {
            if (!_deviceMotors.TryGetValue(deviceId, out var motors))
            {
                motors = new List<MotorState>
                {
                    new MotorState { Id = 1, GroupId = 1, SubId = 1, Position = 2048, Type = "Servo",   Status = "Normal", Velocity = 0.5, MinAngle = 0,   MaxAngle = 180, MinRaw = 0, MaxRaw = 3072 },
                    new MotorState { Id = 2, GroupId = 1, SubId = 2, Position = 768,  Type = "DC",      Status = "Error",  Velocity = 1.0, MinAngle = 0,   MaxAngle = 180, MinRaw = 0, MaxRaw = 3072 },
                    new MotorState { Id = 3, GroupId = 2, SubId = 1, Position = 0,    Type = "Stepper", Status = "Normal", Velocity = 0.2, MinAngle = -90, MaxAngle = 90,  MinRaw = 0, MaxRaw = 4095 }
                };
                _deviceMotors[deviceId] = motors;
                _deviceMotorTick[deviceId] = 0;
            }
            return motors;
        }

        private PongStatus GetPingStatus(int deviceId)
        {
            if (!_devicePingStatuses.TryGetValue(deviceId, out var status))
            {
                status = new PongStatus(BinaryPingState.Stopped, 0, 0, 0);
                _devicePingStatuses[deviceId] = status;
            }

            return status;
        }

        // ── Binary 진입점 ─────────────────────────────────────────────

        public byte[]? ProcessBinaryCommand(byte[] data)
        {
            try
            {
                if (!BinaryDeserializer.TryParseRequestHeader(data, out var hdr)) return null;

                // 브로드캐스트는 응답 없음
                if (hdr.TarId == BinaryProtocolConst.BroadcastId) return null;

                int payloadStart = BinaryProtocolConst.RequestHeaderSize;
                int payloadLen   = Math.Min(hdr.PayloadLen, data.Length - payloadStart);
                var payload      = data.AsSpan(payloadStart, payloadLen);

                return hdr.Cmd switch
                {
                    BinaryCommand.Ping          => HandlePing(hdr),
                    BinaryCommand.Move          => HandleMove(hdr, payload),
                    BinaryCommand.MotionCtrl    => HandleMotionCtrl(hdr, payload),
                    BinaryCommand.GetMotors     => HandleGetMotors(hdr),
                    BinaryCommand.GetMotorState => HandleGetMotorState(hdr),
                    BinaryCommand.GetFiles      => HandleGetFiles(hdr),
                    BinaryCommand.GetFile       => HandleGetFile(hdr, payload),
                    BinaryCommand.SaveFile      => HandleSaveFile(hdr, payload),
                    BinaryCommand.VerifyFile    => HandleVerifyFile(hdr, payload),
                    _                           => BuildErrorResponse(hdr.TarId, hdr.SrcId, hdr.Cmd,
                                                       BinaryErrorCode.UnknownCmd,
                                                       $"Unknown command: 0x{(byte)hdr.Cmd:X2}"),
                };
            }
            catch (Exception ex)
            {
                return BuildErrorResponse(0, 0, BinaryCommand.Error, BinaryErrorCode.Unknown, ex.Message);
            }
        }

        // ── 명령 핸들러 ──────────────────────────────────────────────────

        private byte[] HandlePing(RequestHeader hdr)
        {
            // PONG: 헤더만, payload 없음
            var status = GetPingStatus(hdr.TarId);
            var payload = new byte[BinaryProtocolConst.PongPayloadSize];
            payload[0] = (byte)status.State;
            payload[1] = status.InitState;
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(2), status.CurrentMs);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(6), status.TotalMs);
            return BuildOkResponse(hdr.TarId, hdr.SrcId, BinaryCommand.Pong, payload);
        }

        private byte[] HandleMove(RequestHeader hdr, ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 3)
                return BuildErrorResponse(hdr.TarId, hdr.SrcId, hdr.Cmd, BinaryErrorCode.InvalidParam, "MOVE: payload too short");

            byte   motorId = payload[0];
            ushort pos     = BinaryPrimitives.ReadUInt16LittleEndian(payload[1..]);

            var motors = GetMotors(hdr.TarId);
            var motor  = motors.FirstOrDefault(m => m.Id == motorId);
            if (motor != null) motor.Position = pos;

            // 응답 payload: device_id(1) + motor_id(1)
            var respPayload = new byte[] { hdr.TarId, motorId };
            return BuildOkResponse(hdr.TarId, hdr.SrcId, BinaryCommand.Move, respPayload);
        }

        private byte[] HandleMotionCtrl(RequestHeader hdr, ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 1)
                return BuildErrorResponse(hdr.TarId, hdr.SrcId, hdr.Cmd, BinaryErrorCode.InvalidParam, "MOTION_CTRL: payload too short");

            byte action = payload[0];
            var status = GetPingStatus(hdr.TarId);

            switch ((BinaryMotionAction)action)
            {
                case BinaryMotionAction.Play:
                    status = status with { State = BinaryPingState.Playing };
                    break;
                case BinaryMotionAction.Stop:
                    status = status with { State = BinaryPingState.Stopped, CurrentMs = 0 };
                    break;
                case BinaryMotionAction.Pause:
                    status = status with { State = BinaryPingState.Stopped };
                    break;
                case BinaryMotionAction.Seek:
                    if (payload.Length >= 5)
                    {
                        uint currentMs = BinaryPrimitives.ReadUInt32LittleEndian(payload[1..]);
                        status = status with { CurrentMs = currentMs };
                    }
                    break;
            }

            _devicePingStatuses[hdr.TarId] = status;

            // 응답 payload: action(1) + device_id(1)
            var respPayload = new byte[] { action, hdr.TarId };
            return BuildOkResponse(hdr.TarId, hdr.SrcId, BinaryCommand.MotionCtrl, respPayload);
        }

        private byte[] HandleGetMotors(RequestHeader hdr)
        {
            var motors = GetMotors(hdr.TarId);
            // payload: motor_count(1) + N×17
            var buf    = new byte[1 + motors.Count * 17];
            buf[0]     = (byte)motors.Count;
            int offset = 1;

            foreach (var m in motors)
            {
                buf[offset]     = (byte)m.Id;
                buf[offset + 1] = (byte)m.GroupId;
                buf[offset + 2] = (byte)m.SubId;
                buf[offset + 3] = BinaryDeserializer.EncodeMotorType(m.Type);
                buf[offset + 4] = BinaryDeserializer.EncodeMotorStatus(m.Status);

                ushort pos = (ushort)Math.Clamp(Math.Round(m.Position), 0, 65535);
                BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(offset + 5), pos);

                ushort vel = (ushort)Math.Clamp(Math.Round(m.Velocity * 100), 0, 65535);
                BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(offset + 7), vel);

                short minAngle = (short)Math.Clamp(Math.Round(m.MinAngle * 10), short.MinValue, short.MaxValue);
                BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(offset + 9), minAngle);

                short maxAngle = (short)Math.Clamp(Math.Round(m.MaxAngle * 10), short.MinValue, short.MaxValue);
                BinaryPrimitives.WriteInt16LittleEndian(buf.AsSpan(offset + 11), maxAngle);

                ushort minRaw = (ushort)Math.Clamp(Math.Round(m.MinRaw), 0, 65535);
                BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(offset + 13), minRaw);

                ushort maxRaw = (ushort)Math.Clamp(Math.Round(m.MaxRaw), 0, 65535);
                BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(offset + 15), maxRaw);

                offset += 17;
            }

            return BuildOkResponse(hdr.TarId, hdr.SrcId, BinaryCommand.GetMotors, buf);
        }

        private byte[] HandleGetMotorState(RequestHeader hdr)
        {
            var motors = GetMotors(hdr.TarId);

            // 기존 tick 기반 시뮬레이션 로직 유지
            int tick = _deviceMotorTick.TryGetValue(hdr.TarId, out var t) ? t + 1 : 1;
            _deviceMotorTick[hdr.TarId] = tick;

            foreach (var m in motors)
            {
                var step    = (tick % 5) * 32;
                var nextRaw = m.Position + step;
                if (nextRaw > m.MaxRaw) nextRaw = m.MinRaw;
                m.Position = nextRaw;
            }

            // 홀수 tick: 부분 응답 (Motor 1, 3만), 짝수 tick: 전체
            IEnumerable<MotorState> subset = tick % 2 == 0
                ? motors
                : motors.Where(m => m.Id == 1 || m.Id == 3);

            var subsetList = subset.ToList();
            var buf        = new byte[1 + subsetList.Count * 6];
            buf[0]         = (byte)subsetList.Count;
            int offset     = 1;

            foreach (var m in subsetList)
            {
                buf[offset] = (byte)m.Id;
                ushort pos  = (ushort)Math.Clamp(Math.Round(m.Position), 0, 65535);
                BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(offset + 1), pos);
                ushort vel  = (ushort)Math.Clamp(Math.Round(m.Velocity * 100), 0, 65535);
                BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(offset + 3), vel);
                buf[offset + 5] = BinaryDeserializer.EncodeMotorStatus(m.Status);
                offset += 6;
            }

            return BuildOkResponse(hdr.TarId, hdr.SrcId, BinaryCommand.GetMotorState, buf);
        }

        private byte[] HandleGetFiles(RequestHeader hdr)
        {
            var fileSystem = GetFileSystem(hdr.TarId);
            var payload    = BuildGetFilesPayload(fileSystem);
            return BuildOkResponse(hdr.TarId, hdr.SrcId, BinaryCommand.GetFiles, payload);
        }

        private byte[] HandleGetFile(RequestHeader hdr, ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 2)
                return BuildErrorResponse(hdr.TarId, hdr.SrcId, hdr.Cmd, BinaryErrorCode.InvalidParam, "GET_FILE: payload too short");

            ushort pathLen = BinaryPrimitives.ReadUInt16LittleEndian(payload);
            if (payload.Length < 2 + pathLen)
                return BuildErrorResponse(hdr.TarId, hdr.SrcId, hdr.Cmd, BinaryErrorCode.InvalidParam, "GET_FILE: truncated path");

            string path       = Encoding.UTF8.GetString(payload.Slice(2, pathLen));
            var    fileSystem = GetFileSystem(hdr.TarId);

            if (!fileSystem.TryGetValue(path, out var content))
                return BuildErrorResponse(hdr.TarId, hdr.SrcId, hdr.Cmd, BinaryErrorCode.FileNotFound, "File not found");

            var respPayload = BuildPathContentPayload(path, content);
            return BuildOkResponse(hdr.TarId, hdr.SrcId, BinaryCommand.GetFile, respPayload);
        }

        private byte[] HandleSaveFile(RequestHeader hdr, ReadOnlySpan<byte> payload)
        {
            var (path, content, ok) = ParsePathContent(payload);
            if (!ok || string.IsNullOrEmpty(path))
                return BuildErrorResponse(hdr.TarId, hdr.SrcId, hdr.Cmd, BinaryErrorCode.InvalidParam, "SAVE_FILE: invalid payload");

            var fileSystem        = GetFileSystem(hdr.TarId);
            fileSystem[path]      = content;

            // 응답: path_len(2) + path
            var pathBytes   = Encoding.UTF8.GetBytes(path);
            var respPayload = new byte[2 + pathBytes.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(respPayload.AsSpan(0), (ushort)pathBytes.Length);
            pathBytes.CopyTo(respPayload.AsSpan(2));

            return BuildOkResponse(hdr.TarId, hdr.SrcId, BinaryCommand.SaveFile, respPayload);
        }

        private byte[] HandleVerifyFile(RequestHeader hdr, ReadOnlySpan<byte> payload)
        {
            var (path, content, ok) = ParsePathContent(payload);
            if (!ok)
                return BuildErrorResponse(hdr.TarId, hdr.SrcId, hdr.Cmd, BinaryErrorCode.InvalidParam, "VERIFY_FILE: invalid payload");

            var fileSystem = GetFileSystem(hdr.TarId);
            if (!fileSystem.TryGetValue(path, out var storedContent))
                return BuildErrorResponse(hdr.TarId, hdr.SrcId, hdr.Cmd, BinaryErrorCode.FileNotFound, "File not found");

            string normStored  = storedContent.Replace("\r\n", "\n").Replace("\r", "\n");
            string normCheck   = content.Replace("\r\n", "\n").Replace("\r", "\n");
            bool   match       = normStored == normCheck;

            byte[] pathBytes = Encoding.UTF8.GetBytes(path);
            byte[] respPayload = new byte[2 + pathBytes.Length + 1];
            BinaryPrimitives.WriteUInt16LittleEndian(respPayload.AsSpan(0), (ushort)pathBytes.Length);
            pathBytes.CopyTo(respPayload.AsSpan(2));
            respPayload[^1] = match ? (byte)1 : (byte)0;

            return BuildOkResponse(hdr.TarId, hdr.SrcId, BinaryCommand.VerifyFile, respPayload);
        }

        // ── 응답 빌더 ─────────────────────────────────────────────────

        private static byte[] BuildOkResponse(byte srcId, byte tarId, BinaryCommand cmd, byte[] payload)
        {
            var buf = new byte[BinaryProtocolConst.ResponseHeaderSize + payload.Length];
            buf[0] = srcId;
            buf[1] = tarId;
            buf[2] = (byte)cmd;
            buf[3] = (byte)ResponseStatus.Ok;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), (ushort)payload.Length);
            payload.CopyTo(buf, BinaryProtocolConst.ResponseHeaderSize);
            return buf;
        }

        private static byte[] BuildErrorResponse(byte srcId, byte tarId, BinaryCommand cmd,
            BinaryErrorCode code, string message = "")
        {
            var msgBytes = Encoding.UTF8.GetBytes(message);
            byte msgLen  = (byte)Math.Min(msgBytes.Length, 64);
            var errPayload = new byte[2 + msgLen];
            errPayload[0]  = (byte)code;
            errPayload[1]  = msgLen;
            Array.Copy(msgBytes, 0, errPayload, 2, msgLen);

            var buf = new byte[BinaryProtocolConst.ResponseHeaderSize + errPayload.Length];
            buf[0] = srcId;
            buf[1] = tarId;
            buf[2] = (byte)cmd;
            buf[3] = (byte)ResponseStatus.Error;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(4), (ushort)errPayload.Length);
            errPayload.CopyTo(buf, BinaryProtocolConst.ResponseHeaderSize);
            return buf;
        }

        // ── 페이로드 빌더 헬퍼 ───────────────────────────────────────────

        private static byte[] BuildPathContentPayload(string path, string content)
        {
            var pathBytes    = Encoding.UTF8.GetBytes(path);
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var buf          = new byte[2 + pathBytes.Length + 2 + contentBytes.Length];
            int offset       = 0;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(offset), (ushort)pathBytes.Length); offset += 2;
            pathBytes.CopyTo(buf, offset);                                                           offset += pathBytes.Length;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(offset), (ushort)contentBytes.Length); offset += 2;
            contentBytes.CopyTo(buf, offset);
            return buf;
        }

        private static (string Path, string Content, bool Ok) ParsePathContent(ReadOnlySpan<byte> payload)
        {
            if (payload.Length < 2) return ("", "", false);
            ushort pathLen = BinaryPrimitives.ReadUInt16LittleEndian(payload);
            if (payload.Length < 2 + pathLen + 2) return ("", "", false);
            string path        = Encoding.UTF8.GetString(payload.Slice(2, pathLen));
            int    contentBase = 2 + pathLen;
            ushort contentLen  = BinaryPrimitives.ReadUInt16LittleEndian(payload[contentBase..]);
            if (payload.Length < contentBase + 2 + contentLen) return (path, "", false);
            string content = Encoding.UTF8.GetString(payload.Slice(contentBase + 2, contentLen));
            return (path, content, true);
        }

        // ── GET_FILES: flat list + parent_index 빌드 ──────────────────

        private static byte[] BuildGetFilesPayload(Dictionary<string, string> fileSystem)
        {
            // 기존 트리 빌드 후 DFS 순서로 flat list 생성
            var rootItems = BuildFileSystemTree(fileSystem);
            var flatList  = new List<(bool IsDir, int ParentIdx, long Size, string Name, string Path)>();
            FlattenTree(rootItems, -1, flatList);

            // 총 payload 크기 계산
            var segments = new List<byte[]>(flatList.Count);
            foreach (var (isDir, parentIdx, size, name, path) in flatList)
            {
                var nameBytes = Encoding.UTF8.GetBytes(name);
                var pathBytes = Encoding.UTF8.GetBytes(path);
                // flags(1) + parent_index(2) + size(4) + name_len(1) + name + path_len(2) + path
                var entry = new byte[1 + 2 + 4 + 1 + nameBytes.Length + 2 + pathBytes.Length];
                int o     = 0;
                entry[o]  = isDir ? (byte)1 : (byte)0; o++;
                BinaryPrimitives.WriteInt16LittleEndian(entry.AsSpan(o), (short)parentIdx); o += 2;
                BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(o), (uint)size); o += 4;
                entry[o]  = (byte)nameBytes.Length; o++;
                nameBytes.CopyTo(entry, o); o += nameBytes.Length;
                BinaryPrimitives.WriteUInt16LittleEndian(entry.AsSpan(o), (ushort)pathBytes.Length); o += 2;
                pathBytes.CopyTo(entry, o);
                segments.Add(entry);
            }

            int totalLen = 2 + segments.Sum(s => s.Length);
            var buf      = new byte[totalLen];
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0), (ushort)flatList.Count);
            int offset = 2;
            foreach (var seg in segments)
            {
                seg.CopyTo(buf, offset);
                offset += seg.Length;
            }
            return buf;
        }

        private static void FlattenTree(
            IEnumerable<FileSystemItem> items, int parentIdx,
            List<(bool, int, long, string, string)> result)
        {
            foreach (var item in items)
            {
                int myIdx = result.Count;
                result.Add((item.IsDirectory, parentIdx, item.Size, item.Name, item.Path));
                if (item.IsDirectory && item.Children?.Count > 0)
                    FlattenTree(item.Children, myIdx, result);
            }
        }

        private static List<FileSystemItem> BuildFileSystemTree(Dictionary<string, string> fileSystem)
        {
            var rootItems = new List<FileSystemItem>();
            var dirs      = new Dictionary<string, FileSystemItem>();

            foreach (var kvp in fileSystem)
            {
                string fullPath   = kvp.Key;
                long   size       = kvp.Value.Length;
                string[] parts    = fullPath.Split('/');
                string currentPath = "";
                FileSystemItem? parentDir = null;

                for (int i = 0; i < parts.Length; i++)
                {
                    string part  = parts[i];
                    bool   isFile = (i == parts.Length - 1);
                    currentPath  = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;

                    if (isFile)
                    {
                        var fileItem = new FileSystemItem { Name = part, Path = fullPath, IsDirectory = false, Size = size };
                        if (parentDir != null) { if (!parentDir.Children.Any(c => c.Name == fileItem.Name)) parentDir.Children.Add(fileItem); }
                        else                   { if (!rootItems.Any(r => r.Name == fileItem.Name))            rootItems.Add(fileItem); }
                    }
                    else
                    {
                        if (!dirs.ContainsKey(currentPath))
                        {
                            var newDir = new FileSystemItem { Name = part, Path = currentPath, IsDirectory = true, Size = 0 };
                            dirs[currentPath] = newDir;
                            if (parentDir != null) { if (!parentDir.Children.Any(c => c.Name == newDir.Name)) parentDir.Children.Add(newDir); }
                            else                   { if (!rootItems.Any(r => r.Name == newDir.Name))            rootItems.Add(newDir); }
                        }
                        parentDir = dirs[currentPath];
                    }
                }
            }
            return rootItems;
        }
    }
}
