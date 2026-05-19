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
        Assert.AreEqual(50, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(3)));
        Assert.AreEqual(1, packet[5]);
        Assert.AreEqual(540, BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(6)));
        Assert.AreEqual(schedule.Checksum, BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(8)));
        Assert.AreEqual(7, packet[12]);
        Assert.AreEqual(1, packet[13]);
        Assert.AreEqual(0, packet[14]);
        Assert.AreEqual(540, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(15)));
        Assert.AreEqual(1080, BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(17)));
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
            new OperatingHoursDay("MON", false, 540, 1080),
            new OperatingHoursDay("TUE", false, 540, 1080),
            new OperatingHoursDay("WED", false, 540, 1080),
            new OperatingHoursDay("THU", false, 540, 1080),
            new OperatingHoursDay("FRI", false, 540, 1080),
            new OperatingHoursDay("SAT", true, 0, 0),
            new OperatingHoursDay("SUN", true, 0, 0),
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
