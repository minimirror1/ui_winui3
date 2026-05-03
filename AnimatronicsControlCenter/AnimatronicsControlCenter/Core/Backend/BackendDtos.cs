using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AnimatronicsControlCenter.Core.Backend;

public sealed record BackendStoreCreateRequest(
    [property: JsonPropertyName("store_name")] string StoreName,
    [property: JsonPropertyName("country_code")] string CountryCode,
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("timezone")] string Timezone,
    [property: JsonPropertyName("operate_times")] IReadOnlyList<BackendStoreOperateTime> OperateTimes);

public sealed record BackendStoreOperateTime(
    [property: JsonPropertyName("day_of_week")] string DayOfWeek,
    [property: JsonPropertyName("open_time")] string OpenTime,
    [property: JsonPropertyName("close_time")] string CloseTime);

public sealed record BackendPcCreateRequest(
    [property: JsonPropertyName("pc_name")] string PcName,
    [property: JsonPropertyName("sw_version")] string SwVersion);

public sealed record BackendPcUpdateRequest(
    [property: JsonPropertyName("pc_name")] string PcName,
    [property: JsonPropertyName("sw_version")] string SwVersion);

public sealed record BackendObjectCreateRequest(
    [property: JsonPropertyName("object_name")] string ObjectName,
    [property: JsonPropertyName("object_operation_time")] BackendTimeRange ObjectOperationTime,
    [property: JsonPropertyName("schedule_flag")] bool ScheduleFlag,
    [property: JsonPropertyName("firmware_version")] BackendFirmwareVersion FirmwareVersion,
    [property: JsonPropertyName("operation_status")] string OperationStatus);

public sealed record BackendTimeRange(
    [property: JsonPropertyName("start_time")] string StartTime,
    [property: JsonPropertyName("end_time")] string EndTime);

public sealed record BackendFirmwareVersion(
    [property: JsonPropertyName("board_id")] string BoardId,
    [property: JsonPropertyName("board_type")] string BoardType,
    [property: JsonPropertyName("version")] string Version);

public sealed record BackendObjectLogRequest(
    [property: JsonPropertyName("power_status")] string PowerStatus,
    [property: JsonPropertyName("operation_status")] string OperationStatus,
    [property: JsonPropertyName("power_consumption")] BackendPowerConsumption? PowerConsumption,
    [property: JsonPropertyName("error_data")] IReadOnlyList<BackendErrorData> ErrorData);

public sealed record BackendPowerConsumption(
    [property: JsonPropertyName("volt")] string? Volt,
    [property: JsonPropertyName("ampere")] string? Ampere,
    [property: JsonPropertyName("watt")] string? Watt);

public sealed record BackendErrorData(
    [property: JsonPropertyName("boardId")] string? BoardId,
    [property: JsonPropertyName("boardType")] string? BoardType,
    [property: JsonPropertyName("errorCode")] string? ErrorCode);

public sealed record BackendStoreDetailResponse(
    [property: JsonPropertyName("store_id")] string StoreId,
    [property: JsonPropertyName("store_name")] string? StoreName,
    [property: JsonPropertyName("country_code")] string? CountryCode,
    [property: JsonPropertyName("pcs")] IReadOnlyList<BackendPcDetailResponse> Pcs);

public sealed record BackendPcDetailResponse(
    [property: JsonPropertyName("pc_id")] string PcId,
    [property: JsonPropertyName("pc_name")] string? PcName,
    [property: JsonPropertyName("sw_version")] string? SwVersion,
    [property: JsonPropertyName("objects")] IReadOnlyList<BackendObjectDetailResponse> Objects);

public sealed record BackendObjectDetailResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("object_name")] string? ObjectName,
    [property: JsonPropertyName("power_status")] string? PowerStatus,
    [property: JsonPropertyName("error_data")] IReadOnlyList<BackendErrorData>? ErrorData);

public sealed record BackendStoreListResponse(
    [property: JsonPropertyName("stores")] IReadOnlyList<BackendStoreSummaryResponse> Stores);

public sealed record BackendStoreSummaryResponse(
    [property: JsonPropertyName("id")] string StoreId,
    [property: JsonPropertyName("store_name")] string? StoreName,
    [property: JsonPropertyName("country_code")] string? CountryCode);
