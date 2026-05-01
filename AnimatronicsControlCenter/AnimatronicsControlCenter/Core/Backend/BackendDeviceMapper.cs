using System;
using System.Collections.Generic;
using System.Linq;
using AnimatronicsControlCenter.Core.Models;

namespace AnimatronicsControlCenter.Core.Backend;

public static class BackendDeviceMapper
{
    private static readonly HashSet<string> ErrorStatuses = new(StringComparer.Ordinal)
    {
        "Error",
        "Overload",
        "Disconnected"
    };

    public static BackendObjectLogRequest CreateObjectLog(Device device)
    {
        return new BackendObjectLogRequest(
            PowerStatus: device.PowerStatus == "ON" ? "ON" : "OFF",
            OperationStatus: device.MotionState == MotionState.Playing ? "PLAY" : "STOP",
            PowerConsumption: null,
            ErrorData: device.Motors
                .Where(motor => ErrorStatuses.Contains(motor.Status))
                .Select(CreateErrorData)
                .ToArray());
    }

    private static BackendErrorData CreateErrorData(MotorState motor)
    {
        string boardId = motor.GroupId != 0 || motor.SubId != 0
            ? $"{motor.GroupId}-{motor.SubId}"
            : motor.Id.ToString();

        return new BackendErrorData(boardId, motor.Type, motor.Status);
    }
}
