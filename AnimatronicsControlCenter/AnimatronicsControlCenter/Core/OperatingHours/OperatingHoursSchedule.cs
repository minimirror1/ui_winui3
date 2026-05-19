using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using AnimatronicsControlCenter.Core.Backend;

namespace AnimatronicsControlCenter.Core.OperatingHours;

public sealed record OperatingHoursSchedule(
    string StoreId,
    string? StoreName,
    string? Timezone,
    string? ModifiedAt,
    IReadOnlyList<OperatingHoursDay> Days,
    uint Checksum)
{
    private static readonly string[] OrderedDays = ["MON", "TUE", "WED", "THU", "FRI", "SAT", "SUN"];

    public static OperatingHoursSchedule FromStoreDetail(BackendStoreDetailResponse detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        var byDay = (detail.OperateTimes ?? Array.Empty<BackendStoreOperateTime>())
            .GroupBy(row => NormalizeDay(row.DayOfWeek))
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var days = OrderedDays
            .Select(day => byDay.TryGetValue(day, out var row)
                ? OperatingHoursDay.FromBackend(day, row.OpenTime, row.CloseTime)
                : OperatingHoursDay.Closed(day))
            .ToArray();

        uint checksum = ComputeChecksum(detail.Timezone, detail.ModifiedAt, days);
        return new OperatingHoursSchedule(detail.StoreId, detail.StoreName, detail.Timezone, detail.ModifiedAt, days, checksum);
    }

    public static uint ComputeChecksum(string? timezone, string? modifiedAt, IReadOnlyList<OperatingHoursDay> days)
    {
        var builder = new StringBuilder();
        builder.Append(timezone ?? string.Empty).Append('|');
        if (!string.IsNullOrWhiteSpace(modifiedAt))
        {
            builder.Append(modifiedAt);
        }

        foreach (var day in days.OrderBy(day => Array.IndexOf(OrderedDays, NormalizeDay(day.DayOfWeek))))
        {
            builder
                .Append('|')
                .Append(NormalizeDay(day.DayOfWeek))
                .Append(':')
                .Append(day.IsClosed ? '1' : '0')
                .Append(':')
                .Append(day.OpenMinutes.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(day.CloseMinutes.ToString(CultureInfo.InvariantCulture));
        }

        return Fnv1a32(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static uint Fnv1a32(ReadOnlySpan<byte> data)
    {
        const uint OffsetBasis = 2166136261;
        const uint Prime = 16777619;

        uint hash = OffsetBasis;
        foreach (byte value in data)
        {
            hash ^= value;
            hash *= Prime;
        }

        return hash;
    }

    private static string NormalizeDay(string dayOfWeek)
        => dayOfWeek.Trim().ToUpperInvariant();
}

public sealed record OperatingHoursDay(
    string DayOfWeek,
    bool IsClosed,
    ushort OpenMinutes,
    ushort CloseMinutes)
{
    public static OperatingHoursDay FromBackend(string dayOfWeek, string openTime, string closeTime)
    {
        ushort openMinutes = ParseMinutes(openTime);
        ushort closeMinutes = ParseMinutes(closeTime);
        bool isClosed = openMinutes == 0 && closeMinutes == 0;
        return new OperatingHoursDay(dayOfWeek, isClosed, openMinutes, closeMinutes);
    }

    public static OperatingHoursDay Closed(string dayOfWeek)
        => new(dayOfWeek, true, 0, 0);

    private static ushort ParseMinutes(string value)
    {
        if (!TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out var time))
        {
            throw new FormatException($"Invalid operating-hours time: {value}");
        }

        return checked((ushort)Math.Round(time.TotalMinutes));
    }
}

public sealed record OperatingHoursDeviceSchedule(
    int TimezoneOffsetMinutes,
    uint Checksum,
    IReadOnlyList<OperatingHoursDay> Days);
