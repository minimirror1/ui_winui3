using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AnimatronicsControlCenter.Core.Backend;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnimatronicsControlCenter.UI.ViewModels;

public partial class BackendObjectMappingEditorViewModel : ObservableObject
{
    [ObservableProperty] private string validationMessage = string.Empty;

    public ObservableCollection<BackendObjectMappingEditorRow> ServerObjectMappings { get; } = new();
    public ObservableCollection<BackendObjectMappingEditorRow> MissingServerObjectMappings { get; } = new();

    public bool HasServerObjects => ServerObjectMappings.Count > 0;
    public bool HasMissingServerObjects => MissingServerObjectMappings.Count > 0;
    public bool HasNoServerObjects => !HasServerObjects;

    public BackendObjectMappingEditorViewModel(
        IEnumerable<BackendServerObjectMappingSource> serverObjects,
        IReadOnlyDictionary<int, string> currentMappings)
    {
        var sources = serverObjects
            .Where(source => !string.IsNullOrWhiteSpace(source.ObjectId))
            .GroupBy(source => source.ObjectId)
            .Select(group => group.First())
            .ToList();

        foreach (BackendServerObjectMappingSource source in sources)
        {
            ServerObjectMappings.Add(new BackendObjectMappingEditorRow(
                source.ObjectId,
                source.ObjectName,
                FindLocalObjectIdText(currentMappings, source.ObjectId)));
        }

        var sourceIds = sources.Select(source => source.ObjectId).ToHashSet();
        foreach (KeyValuePair<int, string> mapping in currentMappings.OrderBy(mapping => mapping.Key))
        {
            if (!sourceIds.Contains(mapping.Value))
            {
                MissingServerObjectMappings.Add(new BackendObjectMappingEditorRow(
                    mapping.Value,
                    null,
                    mapping.Key.ToString()));
            }
        }
    }

    public bool TryBuildMappings(out Dictionary<int, string> mappings)
    {
        mappings = new Dictionary<int, string>();
        ValidationMessage = string.Empty;

        foreach (BackendObjectMappingEditorRow row in ServerObjectMappings.Concat(MissingServerObjectMappings))
        {
            string value = row.LocalObjectIdText.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!int.TryParse(value, out int localObjectId))
            {
                ValidationMessage = "WinUI 로컬 오브제 ID는 숫자로 입력해야 합니다.";
                mappings.Clear();
                return false;
            }

            if (mappings.ContainsKey(localObjectId))
            {
                ValidationMessage = "중복된 WinUI 로컬 오브제 ID가 있습니다.";
                mappings.Clear();
                return false;
            }

            mappings[localObjectId] = row.ObjectId;
        }

        return true;
    }

    private static string FindLocalObjectIdText(IReadOnlyDictionary<int, string> mappings, string objectId)
    {
        foreach (KeyValuePair<int, string> mapping in mappings)
        {
            if (mapping.Value == objectId)
            {
                return mapping.Key.ToString();
            }
        }

        return string.Empty;
    }
}

public partial class BackendObjectMappingEditorRow : ObservableObject
{
    [ObservableProperty] private string localObjectIdText;

    public BackendObjectMappingEditorRow(string objectId, string? objectName, string localObjectIdText)
    {
        ObjectId = objectId;
        ObjectName = objectName;
        this.localObjectIdText = localObjectIdText;
    }

    public string ObjectId { get; }
    public string? ObjectName { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(ObjectName) ? ObjectId : $"{ObjectName} ({ObjectId})";
}
