using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace RailStudio.Views
{
    /// <summary>
    /// Module selection item for the dialog.
    /// </summary>
    public class ModuleSelectionItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        
        public string ModuleName { get; set; } = string.Empty;
        public string ModulePath { get; set; } = string.Empty;
        public int ToolCount { get; set; }
        public bool IsExecutable { get; set; }
        
        /// <summary>
        /// Language of this module (csharp, cpp, java, etc.)
        /// Used for polyglot project support.
        /// </summary>
        public string Language { get; set; } = "csharp";
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
    }
    
    /// <summary>
    /// Dialog for selecting which modules to include in the composite manifest.
    /// Enterprise UX: Shows all discovered modules with tool counts, lets user select.
    /// </summary>
    public partial class ModuleSelectionDialog : Window
    {
        public List<ModuleSelectionItem> Modules { get; private set; }
        public List<ModuleSelectionItem> SelectedModules => Modules.Where(m => m.IsSelected).ToList();
        
        public ModuleSelectionDialog(List<ModuleSelectionItem> modules)
        {
            InitializeComponent();
            Modules = modules;
            
            // Bind to ItemsControl
            ModuleItemsControl.ItemsSource = Modules;
            
            // Update summary
            UpdateSummary();
            
            // Subscribe to selection changes
            foreach (var module in Modules)
            {
                module.PropertyChanged += (s, e) => UpdateSummary();
            }
        }
        
        private void UpdateSummary()
        {
            var selected = Modules.Count(m => m.IsSelected);
            var totalTools = Modules.Where(m => m.IsSelected).Sum(m => m.ToolCount);
            SummaryText.Text = $"Selected: {selected} of {Modules.Count} modules | Total tools: {totalTools}";
        }
        
        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            bool allSelected = Modules.All(m => m.IsSelected);
            foreach (var module in Modules)
            {
                module.IsSelected = !allSelected;
            }
        }
        
        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (!Modules.Any(m => m.IsSelected))
            {
                MessageBox.Show("Please select at least one module.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            DialogResult = true;
            Close();
        }
    }
}




