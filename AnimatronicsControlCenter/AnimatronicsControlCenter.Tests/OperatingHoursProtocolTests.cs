using System.Buffers.Binary;
using AnimatronicsControlCenter.Core.OperatingHours;
using AnimatronicsControlCenter.Core.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class OperatingHoursProtocolTests
{
    [TestMethod]
    public void EncodeSetOperateTime_UsesCommandAndFixedSchedulePayload()
    {
        var schedule = TestSchedule();

        byte[] packet = BinarySerializer.EncodeSetOperateTime(0, 7, schedule, 540);

        Assert.AreEqual((byte)BinaryCommand.SetOperateTime, packet[2]);
        Assert.AreEqual(43, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(3)));
        Assert.AreEqual(1, packet[5]);
        Assert.AreEqual(540, BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(6)));
        Assert.AreEqual(schedule.Checksum, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8)));
        Assert.AreEqual(7, packet[12]);
        Assert.AreEqual(1, packet[13]);
        Assert.AreEqual(540, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(14)));
        Assert.AreEqual(1080, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(16)));
        Assert.AreEqual(6, packet[38]);
        Assert.AreEqual(0, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(39)));
        Assert.AreEqual(0, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(41)));
    }

    [TestMethod]
    public void EncodeGetOperateTime_HasNoPayload()
    {
        byte[] packet = BinarySerializer.EncodeGetOperateTime(0, 7);

        Assert.AreEqual((byte)BinaryCommand.GetOperateTime, packet[2]);
        Assert.AreEqual(0, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(3)));
    }

    [TestMethod]
    public void ParseOperatingHoursPayload_ReturnsChecksumAndDays()
    {
        var schedule = TestSchedule();
        byte[] packet = BinarySerializer.EncodeSetOperateTime(0, 7, schedule, 540);
        var payload = packet.AsSpan(BinaryProtocolConst.RequestHeaderSize);

        var parsed = BinaryDeserializer.ParseOperatingHoursPayload(payload);

        Assert.AreEqual(540, parsed.TimezoneOffsetMinutes);
        Assert.AreEqual(schedule.Checksum, parsed.Checksum);
        Assert.AreEqual(7, parsed.Days.Count);
        Assert.AreEqual("MON", parsed.Days[0].DayOfWeek);
        Assert.IsTrue(parsed.Days[5].IsClosed);
    }

    [TestMethod]
    public void ParseOperatingHoursPayload_RejectsLegacyPayloadWithClosedFlag()
    {
        byte[] legacyPayload = new byte[50];
        legacyPayload[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(legacyPayload.AsSpan(1), 540);
        BinaryPrimitives.WriteUInt32LittleEndian(legacyPayload.AsSpan(3), 1234u);
        legacyPayload[7] = 7;

        int offset = 8;
        for (byte day = 1; day <= 7; day++)
        {
            legacyPayload[offset] = day;
            legacyPayload[offset + 1] = day >= 6 ? (byte)1 : (byte)0;
            BinaryPrimitives.WriteUInt16LittleEndian(legacyPayload.AsSpan(offset + 2), day >= 6 ? (ushort)0 : (ushort)540);
            BinaryPrimitives.WriteUInt16LittleEndian(legacyPayload.AsSpan(offset + 4), day >= 6 ? (ushort)0 : (ushort)1080);
            offset += 6;
        }

        Assert.ThrowsException<ArgumentException>(() => BinaryDeserializer.ParseOperatingHoursPayload(legacyPayload));
    }

    [TestMethod]
    public void ParseOperatingHoursPayload_RejectsInvalidDayOfWeek()
    {
        var schedule = TestSchedule();
        byte[] packet = BinarySerializer.EncodeSetOperateTime(0, 7, schedule, 540);
        byte[] payload = packet.AsSpan(BinaryProtocolConst.RequestHeaderSize).ToArray();
        payload[13] = 0;

        Assert.ThrowsException<ArgumentException>(() => BinaryDeserializer.ParseOperatingHoursPayload(payload));
    }

    [TestMethod]
    public void TryParseOperatingHoursPayload_ReturnsFalseForMalformedPayload()
    {
        bool success = BinaryDeserializer.TryParseOperatingHoursPayload(new byte[BinaryProtocolConst.OperatingHoursPayloadSize - 1], out var parsed, out var error);

        Assert.IsFalse(success);
        Assert.IsNull(parsed);
        StringAssert.Contains(error, "Invalid operating-hours payload");
    }

    [TestMethod]
    public void ParseSetOperateTimeResponse_ReturnsChecksum()
    {
        Span<byte> payload = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, 1234u);

        uint checksum = BinaryDeserializer.ParseSetOperateTimeResponse(payload);

        Assert.AreEqual(1234u, checksum);
    }

    private static OperatingHoursSchedule TestSchedule()
    {
        var days = new[]
        {
            new OperatingHoursDay("MON", 540, 1080),
            new OperatingHoursDay("TUE", 540, 1080),
            new OperatingHoursDay("WED", 540, 1080),
            new OperatingHoursDay("THU", 540, 1080),
            new OperatingHoursDay("FRI", 540, 1080),
            new OperatingHoursDay("SAT", 0, 0),
            new OperatingHoursDay("SUN", 0, 0),
        };

        return new OperatingHoursSchedule(
            "store-1",
            "Seoul Store",
            "Asia/Seoul",
            "2026-02-26T01:49:43.727Z",
            days,
            OperatingHoursSchedule.ComputeChecksum("Asia/Seoul", "2026-02-26T01:49:43.727Z", days));
    }
}
