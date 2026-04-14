using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using AnimatronicsControlCenter.Core.Motors;

namespace AnimatronicsControlCenter.Core.Protocol;

public static class BinaryDeserializer
{
    // ── 응답 헤더 파싱 ────────────────────────────────────────────────

    public static bool TryParseResponseHeader(ReadOnlySpan<byte> data, out ResponseHeader header)
    {
        header = default;
        if (data.Length < BinaryProtocolConst.ResponseHeaderSize) return false;
        header = new ResponseHeader(
            SrcId:      data[0],
            TarId:      data[1],
            Cmd:        (BinaryCommand)data[2],
            Status:     (ResponseStatus)data[3],
            PayloadLen: BinaryPrimitives.ReadUInt16LittleEndian(data[4..]));
        return true;
    }

    /// 응답이 OK 상태인지 확인
    public static bool IsOk(ResponseHeader header) => header.Status == ResponseStatus.Ok;

    // ── 요청 헤더 파싱 (VirtualDeviceManager용) ──────────────────────

    public static bool TryParseRequestHeader(ReadOnlySpan<byte> data, out RequestHeader header)
    {
        header = default;
        if (data.Length < BinaryProtocolConst.RequestHeaderSize) return false;
        header = new RequestHeader(
            SrcId:      data[0],
            TarId:      data[1],
            Cmd:        (BinaryCommand)data[2],
            PayloadLen: BinaryPrimitives.ReadUInt16LittleEndian(data[3..]));
        return true;
    }

    // ── §4.5 GET_MOTOR_STATE 응답 ─────────────────────────────────────

    /// byte[] → MotorStatePatch 리스트 (부분 응답 지원)
    public static List<MotorStatePatch> ParseMotorStateResponse(ReadOnlySpan<byte> payload)
    {
        var result = new List<MotorStatePatch>();
        if (payload.IsEmpty) return result;

        int motorCount = payload[0];
        int offset = 1;
        const int EntrySize = 6;

        for (int i = 0; i < motorCount && offset + EntrySize <= payload.Length; i++)
        {
            byte   id       = payload[offset];
            ushort position = BinaryPrimitives.ReadUInt16LittleEndian(payload[(offset + 1)..]);
            ushort velocity = BinaryPrimitives.ReadUInt16LittleEndian(payload[(offset + 3)..]);
            byte   status   = payload[offset + 5];

            result.Add(new MotorStatePatch
            {
                Id       = id,
                Position = position,
                Velocity = velocity / 100.0,
                Status   = DecodeMotorStatus(status),
            });
            offset += EntrySize;
        }
        return result;
    }

    // ── §4.4 GET_MOTORS 응답 ─────────────────────────────────────────

    public static List<MotorStatePatch> ParseGetMotorsResponse(ReadOnlySpan<byte> payload)
    {
        var result = new List<MotorStatePatch>();
        if (payload.IsEmpty) return result;

        int motorCount = payload[0];
        int offset = 1;
        const int EntrySize = 17;

        for (int i = 0; i < motorCount && offset + EntrySize <= payload.Length; i++)
        {
            byte   id       = payload[offset];
            byte   groupId  = payload[offset + 1];
            byte   subId    = payload[offset + 2];
            byte   type     = payload[offset + 3];
            byte   status   = payload[offset + 4];
            ushort position = BinaryPrimitives.ReadUInt16LittleEndian(payload[(offset + 5)..]);
            ushort velocity = BinaryPrimitives.ReadUInt16LittleEndian(payload[(offset + 7)..]);
            short  minAngle = BinaryPrimitives.ReadInt16LittleEndian(payload[(offset + 9)..]);
            short  maxAngle = BinaryPrimitives.ReadInt16LittleEndian(payload[(offset + 11)..]);
            ushort minRaw   = BinaryPrimitives.ReadUInt16LittleEndian(payload[(offset + 13)..]);
            ushort maxRaw   = BinaryPrimitives.ReadUInt16LittleEndian(payload[(offset + 15)..]);

            result.Add(new MotorStatePatch
            {
                Id       = id,
                GroupId  = groupId,
                SubId    = subId,
                Type     = DecodeMotorType(type),
                Status   = DecodeMotorStatus(status),
                Position = position,
                Velocity = velocity / 100.0,
                MinAngle = minAngle / 10.0,
                MaxAngle = maxAngle / 10.0,
                MinRaw   = minRaw,
                MaxRaw   = maxRaw,
            });
            offset += EntrySize;
        }
        return result;
    }

    // ── §4.6 GET_FILES 응답 ──────────────────────────────────────────

    public record FileEntry(bool IsDirectory, int ParentIndex, long Size, string Name, string Path);

    public static List<FileEntry> ParseGetFilesResponse(ReadOnlySpan<byte> payload)
    {
        var result = new List<FileEntry>();
        if (payload.Length < 2) return result;

        ushort entryCount = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        int    offset     = 2;

        for (int i = 0; i < entryCount && offset < payload.Length; i++)
        {
            if (offset + 8 > payload.Length) break;

            byte   flags     = payload[offset];
            bool   isDir     = (flags & 0x01) != 0;
            short  parentIdx = BinaryPrimitives.ReadInt16LittleEndian(payload[(offset + 1)..]);
            uint   size      = BinaryPrimitives.ReadUInt32LittleEndian(payload[(offset + 3)..]);
            byte   nameLen   = payload[offset + 7];
            offset += 8;

            if (offset + nameLen > payload.Length) break;
            string name = Encoding.UTF8.GetString(payload.Slice(offset, nameLen));
            offset += nameLen;

            if (offset + 2 > payload.Length) break;
            ushort pathLen = BinaryPrimitives.ReadUInt16LittleEndian(payload[offset..]);
            offset += 2;

            if (offset + pathLen > payload.Length) break;
            string path = Encoding.UTF8.GetString(payload.Slice(offset, pathLen));
            offset += pathLen;

            result.Add(new FileEntry(isDir, parentIdx, size, name, path));
        }
        return result;
    }

    // ── §4.7 GET_FILE 응답 ───────────────────────────────────────────

    public static (string Path, string Content) ParseGetFileResponse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2) return ("", "");
        ushort pathLen = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (payload.Length < 2 + pathLen + 2) return ("", "");
        string path = Encoding.UTF8.GetString(payload.Slice(2, pathLen));
        int    contentOffset = 2 + pathLen;
        ushort contentLen    = BinaryPrimitives.ReadUInt16LittleEndian(payload[contentOffset..]);
        if (payload.Length < contentOffset + 2 + contentLen) return (path, "");
        string content = Encoding.UTF8.GetString(payload.Slice(contentOffset + 2, contentLen));
        return (path, content);
    }

    // ── §4.8 SAVE_FILE 응답 ─────────────────────────────────────────

    public static string ParseSaveFileResponse(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2) return "";
        ushort pathLen = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        if (payload.Length < 2 + pathLen) return "";
        return Encoding.UTF8.GetString(payload.Slice(2, pathLen));
    }

    // ── §4.9 VERIFY_FILE 응답 ────────────────────────────────────────

    public static bool ParseVerifyFileResponse(ReadOnlySpan<byte> payload)
        => payload.Length >= 1 && payload[0] != 0;

    // ── §4.2 MOVE 응답 ──────────────────────────────────────────────

    public static (byte DeviceId, byte MotorId) ParseMoveResponse(ReadOnlySpan<byte> payload)
        => payload.Length >= 2 ? (payload[0], payload[1]) : ((byte)0, (byte)0);

    // ── §4.3 MOTION_CTRL 응답 ────────────────────────────────────────

    public static (BinaryMotionAction Action, byte DeviceId) ParseMotionCtrlResponse(ReadOnlySpan<byte> payload)
        => payload.Length >= 2 ? ((BinaryMotionAction)payload[0], payload[1]) : (BinaryMotionAction.Play, (byte)0);

    // ── §4.10 ERROR 응답 ─────────────────────────────────────────────

    public static (BinaryErrorCode Code, string Message) ParseErrorResponse(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty) return (BinaryErrorCode.Unknown, "");
        var  code   = (BinaryErrorCode)payload[0];
        if (payload.Length < 2) return (code, "");
        byte msgLen = payload[1];
        if (payload.Length < 2 + msgLen) return (code, "");
        string msg = Encoding.UTF8.GetString(payload.Slice(2, msgLen));
        return (code, msg);
    }

    // ── Enum 디코딩 헬퍼 ─────────────────────────────────────────────

    public static string DecodeMotorType(byte b) => b switch
    {
        0x00 => "Servo",
        0x01 => "DC",
        0x02 => "Stepper",
        _    => "Unknown",
    };

    public static string DecodeMotorStatus(byte b) => b switch
    {
        0x00 => "Normal",
        0x01 => "Error",
        0x02 => "Overload",
        0x03 => "Disconnected",
        _    => "Unknown",
    };

    public static byte EncodeMotorType(string type) => type switch
    {
        "Servo"   => 0x00,
        "DC"      => 0x01,
        "Stepper" => 0x02,
        _         => 0x00,
    };

    public static byte EncodeMotorStatus(string status) => status switch
    {
        "Normal"       => 0x00,
        "Error"        => 0x01,
        "Overload"     => 0x02,
        "Disconnected" => 0x03,
        _              => 0x00,
    };
}
