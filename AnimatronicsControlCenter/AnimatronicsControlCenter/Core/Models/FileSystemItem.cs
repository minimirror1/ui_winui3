using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace AnimatronicsControlCenter.Core.Models
{
    public partial class FileSystemItem : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string path = string.Empty;

        [ObservableProperty]
        [JsonPropertyName("isDirectory")]
        private bool isDirectory;

        [ObservableProperty]
        private long size;

        // Properties for tree structure building (from JSON response)
        [JsonPropertyName("depth")]
        public int Depth { get; set; }
        
        [JsonPropertyName("parentIndex")]
        public int ParentIndex { get; set; } = -1; // Default to -1 (root) if not specified

        public ObservableCollection<FileSystemItem> Children { get; set; } = new();
        
        // Helper property for UI binding if needed
        public string Icon => IsDirectory ? "\uE8B7" : "\uE7C3"; // Segoe MDL2 Assets: Folder vs File
    }
}













