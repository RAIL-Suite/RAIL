using CommunityToolkit.Mvvm.ComponentModel;

namespace RailStudio.ViewModels
{
    /// <summary>
    /// Wrapper for ToolFunctionModel that adds selection state for UI checkboxes.
    /// </summary>
    public partial class SelectableToolFunction : ObservableObject
    {
        [ObservableProperty]
        private bool _isSelected;

        public ToolFunctionModel Model { get; }

        public SelectableToolFunction(ToolFunctionModel model)
        {
            Model = model;
            _isSelected = false;
        }
    }
}




