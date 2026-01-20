using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RailStudio.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RailStudio.ViewModels
{
    public partial class DuplicatesManagerViewModel : ObservableObject
    {
        private readonly IManifestService _manifestService;
        private readonly IManifestBackupService _backupService;
        private readonly string _manifestPath;

        public ObservableCollection<SelectableToolFunction> Duplicates { get; }

        [ObservableProperty]
        private string _statusMessage;

        [ObservableProperty]
        private int _selectedCount;

        [ObservableProperty]
        private bool _isBusy;

        public DuplicatesManagerViewModel(
            IManifestService manifestService,
            IManifestBackupService backupService,
            string manifestPath,
            ObservableCollection<ToolFunctionModel> duplicateModels)
        {
            _manifestService = manifestService;
            _backupService = backupService;
            _manifestPath = manifestPath;

            Duplicates = new ObservableCollection<SelectableToolFunction>(
                duplicateModels.Select(m => new SelectableToolFunction(m))
            );

            // Subscribe to selection changes
            foreach (var item in Duplicates)
            {
                item.PropertyChanged += Item_PropertyChanged;
            }

            UpdateSelectionState();
            StatusMessage = $"{Duplicates.Count} duplicate(s) found.";
        }

        private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableToolFunction.IsSelected))
            {
                UpdateSelectionState();
            }
        }

        private void UpdateSelectionState()
        {
            SelectedCount = Duplicates.Count(d => d.IsSelected);
            DeleteSelectedCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var item in Duplicates) item.IsSelected = true;
        }

        [RelayCommand]
        private void DeselectAll()
        {
            foreach (var item in Duplicates) item.IsSelected = false;
        }

        [RelayCommand]
        private void Close(Window window)
        {
            window?.Close();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private async Task DeleteSelected()
        {
            if (SelectedCount == 0) return;

            var itemsToDelete = Duplicates.Where(d => d.IsSelected).ToList();
            
            var result = MessageBox.Show(
                $"Are you sure you want to delete {SelectedCount} selected function(s)?\nTarget Manifest: {_manifestPath}",
                "Confirm Batch Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            StatusMessage = "Creating backup...";

            try
            {
                // Create backup
                // MessageBox.Show($"Attempting backup of: {_manifestPath}");
                var backupPath = _backupService.CreateBackup(_manifestPath);
                // MessageBox.Show($"Backup created at: {backupPath}");

                StatusMessage = "Deleting functions...";
                int successCount = 0;
                int failCount = 0;

                // Sort by Index descending
                var sortedItems = itemsToDelete.OrderByDescending(x => x.Model.Index).ToList();
                
                foreach (var item in sortedItems)
                {
                    // Log attempt
                    // System.Diagnostics.Debug.WriteLine($"Deleting {item.Model.Name} at Index {item.Model.Index}");

                    var success = await _manifestService.DeleteToolAsync(_manifestPath, item.Model.Name, item.Model.Index);
                    if (success)
                    {
                        successCount++;
                        Duplicates.Remove(item);
                    }
                    else
                    {
                        failCount++;
                        // MessageBox.Show($"Failed to delete {item.Model.Name} at Index {item.Model.Index}. Possible index mismatch?");
                    }
                }

                StatusMessage = $"Deleted: {successCount}, Failed: {failCount}";
                
                if (successCount > 0)
                {
                    //MessageBox.Show($"Successfully deleted {successCount} functions.\nFailed: {failCount}\nBackup: {System.IO.Path.GetFileName(backupPath)}", 
                    //    "Deletion Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Failed to delete any functions.\nCheck if file was modified externally.", 
                        "Deletion Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBox.Show($"Critical Error during deletion:\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
                UpdateSelectionState();
            }
        }

        private bool CanDelete() => SelectedCount > 0;
    }
}




