using System;
using AnimatronicsControlCenter.Core.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class SntpPacketTests
{
    private static readonly DateTimeOffset T1 = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void BuildRequest_Is48BytesWithClientModeHeader()
    {
        byte[] request = SntpPacket.BuildRequest();

        Assert.AreEqual(48, request.Length);
        Assert.AreEqual(0x23, request[0], "LI=0, VN=4, Mode=3(client) 이어야 한다.");
    }

    [TestMethod]
    public void Timestamp_WriteAndRead_RoundTripsWithinOneMillisecond()
    {
        var buffer = new byte[48];
        var time = new DateTimeOffset(2026, 9, 4, 1, 2, 3, 456, TimeSpan.Zero);

        SntpPacket.WriteTimestamp(buffer, 40, time);
        DateTimeOffset roundTripped = SntpPacket.ReadTimestamp(buffer, 40);

        Assert.IsTrue((roundTripped - time).Duration() < TimeSpan.FromMilliseconds(1));
    }

    [TestMethod]
    public void TryParseCorrection_ServerAheadTenSeconds_ReturnsPlusTenCorrection()
    {
        // 서버 시계가 +10초, 편도 지연 50ms(대칭), 서버 처리시간 0.
        var t4 = T1.AddMilliseconds(100);
        var serverReceive = T1.AddMilliseconds(50).AddSeconds(10);
        var serverTransmit = serverReceive;
        byte[] response = BuildServerResponse(serverReceive, serverTransmit);

        bool ok = SntpPacket.TryParseCorrection(response, T1, t4, out TimeSpan correction, out string? error);

        Assert.IsTrue(ok, error);
        Assert.IsTrue((correction - TimeSpan.FromSeconds(10)).Duration() < TimeSpan.FromMilliseconds(5),
            $"correction={correction.TotalMilliseconds}ms");
    }

    [TestMethod]
    public void TryParseCorrection_ClocksInSync_ReturnsNearZero()
    {
        var t4 = T1.AddMilliseconds(80);
        var serverReceive = T1.AddMilliseconds(40);
        byte[] response = BuildServerResponse(serverReceive, serverReceive);

        bool ok = SntpPacket.TryParseCorrection(response, T1, t4, out TimeSpan correction, out _);

        Assert.IsTrue(ok);
        Assert.IsTrue(correction.Duration() < TimeSpan.FromMilliseconds(5));
    }

    [TestMethod]
    public void TryParseCorrection_TooShortResponse_Fails()
    {
        bool ok = SntpPacket.TryParseCorrection(new byte[10], T1, T1.AddMilliseconds(100), out _, out string? error);

        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParseCorrection_NonServerMode_Fails()
    {
        byte[] response = BuildServerResponse(T1, T1);
        response[0] = 0x23; // Mode=3 (client) — 서버 응답이 아님

        bool ok = SntpPacket.TryParseCorrection(response, T1, T1.AddMilliseconds(100), out _, out string? error);

        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParseCorrection_KissOfDeathStratumZero_Fails()
    {
        byte[] response = BuildServerResponse(T1, T1);
        response[1] = 0; // stratum 0 = kiss-of-death

        bool ok = SntpPacket.TryParseCorrection(response, T1, T1.AddMilliseconds(100), out _, out string? error);

        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void TryParseCorrection_ZeroTransmitTimestamp_Fails()
    {
        var response = new byte[48];
        response[0] = 0x24; // LI=0, VN=4, Mode=4(server)
        response[1] = 2;    // stratum 2
        SntpPacket.WriteTimestamp(response, 32, T1);
        // Transmit(offset 40)은 0으로 남김

        bool ok = SntpPacket.TryParseCorrection(response, T1, T1.AddMilliseconds(100), out _, out string? error);

        Assert.IsFalse(ok);
        Assert.IsNotNull(error);
    }

    private static byte[] BuildServerResponse(DateTimeOffset receiveTime, DateTimeOffset transmitTime)
    {
        var response = new byte[48];
        response[0] = 0x24; // LI=0, VN=4, Mode=4(server)
        response[1] = 2;    // stratum 2
        SntpPacket.WriteTimestamp(response, 32, receiveTime);  // Receive Timestamp (T2)
        SntpPacket.WriteTimestamp(response, 40, transmitTime); // Transmit Timestamp (T3)
        return response;
    }
}
