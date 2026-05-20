using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AnimatronicsControlCenter.Core.Interfaces;
using AnimatronicsControlCenter.Core.OperatingHours;

namespace AnimatronicsControlCenter.Infrastructure;

public sealed class OperatingHoursCache : IOperatingHoursCache
{
    private const string FileName = "operating-hours-cache.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IBackendSettingsPathProvider _pathProvider;

    public OperatingHoursCache(IBackendSettingsPathProvider pathProvider)
    {
        _pathProvider = pathProvider;
    }

    public async Task SaveAsync(OperatingHoursSchedule schedule, CancellationToken cancellationToken)
    {
        string filePath = GetFilePath();
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(schedule, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }

    public async Task<OperatingHoursSchedule?> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            string filePath = GetFilePath();
            if (!File.Exists(filePath))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<OperatingHoursSchedule>(json, JsonOptions);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private string GetFilePath()
    {
        string? directory = Path.GetDirectoryName(_pathProvider.BackendSettingsFilePath);
        return string.IsNullOrWhiteSpace(directory)
            ? FileName
            : Path.Combine(directory, FileName);
    }
}
