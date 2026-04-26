using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace AnimatronicsControlCenter.Core.Utilities;

public sealed record SerialPortDeviceInfo(
    string PortName,
    string? FriendlyName,
    string? Manufacturer,
    string? DeviceDescription,
    string? HardwareId);

public sealed record SerialPortOption(
    string PortName,
    string DisplayName,
    bool IsLikelyXBee);

public static class SerialPortDisplay
{
    private static readonly string[] XBeeHints =
    [
        "xbee",
        "digi",
        "xbib",
    ];

    public static SerialPortOption CreateOption(string portName, SerialPortDeviceInfo? deviceInfo)
    {
        string label = BuildDeviceLabel(deviceInfo);
        bool isLikelyXBee = IsLikelyXBeeDevice(deviceInfo);

        if (string.IsNullOrWhiteSpace(label))
        {
            return new SerialPortOption(portName, portName, isLikelyXBee);
        }

        string displayName = isLikelyXBee
            ? $"{portName} (XBee 후보: {label})"
            : $"{portName} ({label})";

        return new SerialPortOption(portName, displayName, isLikelyXBee);
    }

    private static bool IsLikelyXBeeDevice(SerialPortDeviceInfo? deviceInfo)
    {
        if (deviceInfo is null) return false;

        string combined = string.Join(" ", new[]
        {
            deviceInfo.FriendlyName,
            deviceInfo.Manufacturer,
            deviceInfo.DeviceDescription,
            deviceInfo.HardwareId,
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return XBeeHints.Any(hint => combined.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildDeviceLabel(SerialPortDeviceInfo? deviceInfo)
    {
        if (deviceInfo is null) return string.Empty;

        string label = FirstNonEmpty(
            deviceInfo.FriendlyName,
            deviceInfo.DeviceDescription,
            deviceInfo.Manufacturer);

        return RemovePortSuffix(label, deviceInfo.PortName);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string RemovePortSuffix(string value, string portName)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        string suffix = $"({portName})";
        return value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? value[..^suffix.Length].Trim()
            : value.Trim();
    }
}

#pragma warning disable CA1416
public static class SerialPortDeviceInfoProvider
{
    public static IReadOnlyDictionary<string, SerialPortDeviceInfo> GetDeviceInfoByPort(IEnumerable<string> portNames)
    {
        HashSet<string> requestedPorts = portNames
            .Where(port => !string.IsNullOrWhiteSpace(port))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Dictionary<string, SerialPortDeviceInfo> result = new(StringComparer.OrdinalIgnoreCase);
        if (requestedPorts.Count == 0) return result;
        if (!OperatingSystem.IsWindows()) return result;

        try
        {
            using RegistryKey? enumKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum");
            if (enumKey is null) return result;

            CollectPortInfo(enumKey, requestedPorts, result, depth: 0);
        }
        catch
        {
            return result;
        }

        return result;
    }

    private static void CollectPortInfo(
        RegistryKey key,
        HashSet<string> requestedPorts,
        Dictionary<string, SerialPortDeviceInfo> result,
        int depth)
    {
        if (depth > 6 || result.Count == requestedPorts.Count) return;

        TryReadPortInfo(key, requestedPorts, result);

        foreach (string subKeyName in key.GetSubKeyNames())
        {
            try
            {
                using RegistryKey? subKey = key.OpenSubKey(subKeyName);
                if (subKey is not null)
                {
                    CollectPortInfo(subKey, requestedPorts, result, depth + 1);
                }
            }
            catch
            {
            }
        }
    }

    private static void TryReadPortInfo(
        RegistryKey key,
        HashSet<string> requestedPorts,
        Dictionary<string, SerialPortDeviceInfo> result)
    {
        try
        {
            using RegistryKey? parameters = key.OpenSubKey("Device Parameters");
            string? portName = parameters?.GetValue("PortName") as string;
            if (string.IsNullOrWhiteSpace(portName) ||
                !requestedPorts.Contains(portName) ||
                result.ContainsKey(portName))
            {
                return;
            }

            result[portName] = new SerialPortDeviceInfo(
                PortName: portName,
                FriendlyName: key.GetValue("FriendlyName") as string,
                Manufacturer: key.GetValue("Mfg") as string,
                DeviceDescription: key.GetValue("DeviceDesc") as string,
                HardwareId: string.Join(" ", (key.GetValue("HardwareID") as string[]) ?? []));
        }
        catch
        {
        }
    }
}
#pragma warning restore CA1416
