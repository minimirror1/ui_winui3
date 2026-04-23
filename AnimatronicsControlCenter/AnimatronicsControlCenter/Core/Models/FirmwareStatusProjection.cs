using System;
using AnimatronicsControlCenter.Core.Protocol;

namespace AnimatronicsControlCenter.Core.Models;

public static class FirmwareStatusProjection
{
    public static void Apply(Device device, PongStatus status, ulong address64, bool isVirtual = false)
    {
        device.IsConnected = true;
        device.Address64 = address64;
        device.MotionState = MapMotionState(status.State);
        device.MotionCurrentTime = TimeSpan.FromMilliseconds(status.CurrentMs);
        device.MotionTotalTime = TimeSpan.FromMilliseconds(status.TotalMs);
        device.StatusMessage = BuildStatusMessage(status.State, isVirtual);
    }

    private static MotionState MapMotionState(BinaryPingState state) => state switch
    {
        BinaryPingState.Playing => MotionState.Playing,
        BinaryPingState.Stopped => MotionState.Stopped,
        _ => MotionState.Idle,
    };

    private static string BuildStatusMessage(BinaryPingState state, bool isVirtual)
    {
        string message = state switch
        {
            BinaryPingState.Playing => "Playing",
            BinaryPingState.Stopped => "Stopped",
            BinaryPingState.InitBusy => "Initializing",
            BinaryPingState.InitDone => "Ready",
            BinaryPingState.Error => "Error",
            _ => "Unknown",
        };

        return isVirtual ? $"{message} (Virtual)" : message;
    }
}
