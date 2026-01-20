using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RailStudio.Models
{
    public class FileSystemNode : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public string Icon { get; set; } = string.Empty;
        public ObservableCollection<FileSystemNode> Children { get; set; } = new();

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        
        /// <summary>
        /// Indicates if this folder is the active (last built) asset in the Assets panel.
        /// Used for visual highlighting.
        /// </summary>
        public bool IsActiveAsset { get; set; }
        
        /// <summary>
        /// File extension (lowercase, with dot). Empty for directories.
        /// </summary>
        public string Extension => IsDirectory ? string.Empty : System.IO.Path.GetExtension(Path).ToLowerInvariant();
    }
}




