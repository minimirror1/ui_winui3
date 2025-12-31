using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace AnimatronicsControlCenter.Core.Models
{
    public partial class FileSystemItem : ObservableObject
    {
        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string path = string.Empty;

        [ObservableProperty]
        private bool isDirectory;

        [ObservableProperty]
        private long size;

        public ObservableCollection<FileSystemItem> Children { get; set; } = new();
        
        // Helper property for UI binding if needed
        public string Icon => IsDirectory ? "\uE8B7" : "\uE7C3"; // Segoe MDL2 Assets: Folder vs File
    }
}













