using AnimatronicsControlCenter.Core.OperatingHours;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class VirtualDeviceManagerOperatingHoursTests
{
    [TestMethod]
    public void ProcessBinaryCommand_StoresAndReadsOperatingHours()
    {
        var manager = new VirtualDeviceManager();
        var schedule = TestSchedule();
        byte[] setResponse = manager.ProcessBinaryCommand(AnimatronicsControlCenter.Core.Protocol.BinarySerializer.EncodeSetOperateTime(0, 1, schedule, 540))!;

        uint setChecksum = AnimatronicsControlCenter.Core.Protocol.BinaryDeserializer.ParseSetOperateTimeResponse(setResponse.AsSpan(6));
        byte[] getResponse = manager.ProcessBinaryCommand(AnimatronicsControlCenter.Core.Protocol.BinarySerializer.EncodeGetOperateTime(0, 1))!;
        var parsed = AnimatronicsControlCenter.Core.Protocol.BinaryDeserializer.ParseOperatingHoursPayload(getResponse.AsSpan(6));

        Assert.AreEqual(schedule.Checksum, setChecksum);
        Assert.AreEqual(schedule.Checksum, parsed.Checksum);
        Assert.AreEqual(540, parsed.TimezoneOffsetMinutes);
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

        return new OperatingHoursSchedule("store-1", "Seoul Store", "Asia/Seoul", null, days, 1234);
    }
}
