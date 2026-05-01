using System;
using System.Buffers.Binary;
using AnimatronicsControlCenter.Core.Models;
using AnimatronicsControlCenter.Core.Protocol;
using AnimatronicsControlCenter.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AnimatronicsControlCenter.Tests;

[TestClass]
public class FirmwareStatusProjectionTests
{
    [TestMethod]
    public void Apply_MapsPlayingStateAndTimes()
    {
        var device = new Device(7);
        var status = new PongStatus(BinaryPingState.Playing, InitState: 0x03, CurrentMs: 1500, TotalMs: 9000);

        FirmwareStatusProjection.Apply(device, status, address64: 0x1122334455667788UL);

        Assert.IsTrue(device.IsConnected);
        Assert.AreEqual(MotionState.Playing, device.MotionState);
        Assert.AreEqual("Playing", device.StatusMessage);
        Assert.AreEqual(TimeSpan.FromMilliseconds(1500), device.MotionCurrentTime);
        Assert.AreEqual(TimeSpan.FromMilliseconds(9000), device.MotionTotalTime);
        Assert.AreEqual(0x1122334455667788UL, device.Address64);
    }

    [TestMethod]
    public void Apply_MapsInitBusyToInitializingMessage()
    {
        var device = new Device(9);
        var status = new PongStatus(BinaryPingState.InitBusy, InitState: 0x02, CurrentMs: 0, TotalMs: 0);

        FirmwareStatusProjection.Apply(device, status, address64: 0, isVirtual: true);

        Assert.AreEqual(MotionState.Idle, device.MotionState);
        Assert.AreEqual("Initializing (Virtual)", device.StatusMessage);
    }

    [TestMethod]
    public void TryParsePongResponse_ReadsOptionalPowerStatusByte()
    {
        byte[] payload = new byte[11];
        payload[0] = (byte)BinaryPingState.Stopped;
        payload[10] = 0x01;

        bool parsed = BinaryDeserializer.TryParsePongResponse(payload, out var status);

        Assert.IsTrue(parsed);
        Assert.AreEqual("ON", status.PowerStatus);
    }

    [TestMethod]
    public void TryParsePongResponse_MissingPowerStatusByte_DefaultsOff()
    {
        byte[] payload = new byte[10];
        payload[0] = (byte)BinaryPingState.Stopped;

        bool parsed = BinaryDeserializer.TryParsePongResponse(payload, out var status);

        Assert.IsTrue(parsed);
        Assert.AreEqual("OFF", status.PowerStatus);
    }

    [TestMethod]
    public void Apply_ProjectsPowerStatus()
    {
        var device = new Device(7);
        var status = new PongStatus(BinaryPingState.Stopped, InitState: 0, CurrentMs: 0, TotalMs: 0, PowerStatus: "ON");

        FirmwareStatusProjection.Apply(device, status, address64: 0);

        Assert.AreEqual("ON", device.PowerStatus);
    }

    [TestMethod]
    public void VirtualDeviceManager_PingResponse_IsFirmwareShaped()
    {
        var manager = new VirtualDeviceManager();
        byte[] request = BuildPingRequest(srcId: 0, tarId: 4);

        byte[]? response = manager.ProcessBinaryCommand(request);

        Assert.IsNotNull(response);
        Assert.IsTrue(BinaryDeserializer.TryParseResponseHeader(response, out var header));
        Assert.AreEqual(BinaryCommand.Pong, header.Cmd);
        Assert.AreEqual(ResponseStatus.Ok, header.Status);

        ReadOnlySpan<byte> payload = response.AsSpan(BinaryProtocolConst.ResponseHeaderSize, header.PayloadLen);
        Assert.AreEqual(10, payload.Length);
        Assert.AreEqual((byte)BinaryPingState.Stopped, payload[0]);
        Assert.AreEqual((byte)0, payload[1]);
        Assert.AreEqual((uint)0, BinaryPrimitives.ReadUInt32LittleEndian(payload[2..]));
        Assert.AreEqual((uint)0, BinaryPrimitives.ReadUInt32LittleEndian(payload[6..]));
    }

    private static byte[] BuildPingRequest(byte srcId, byte tarId)
    {
        byte[] request = new byte[BinaryProtocolConst.RequestHeaderSize];
        request[0] = srcId;
        request[1] = tarId;
        request[2] = (byte)BinaryCommand.Ping;
        BinaryPrimitives.WriteUInt16LittleEndian(request.AsSpan(3), 0);
        return request;
    }
}
