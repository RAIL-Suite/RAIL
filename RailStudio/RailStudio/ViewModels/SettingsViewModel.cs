using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RailStudio.Models;
using RailStudio.Services;

namespace RailStudio.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private string _outputPath = string.Empty;
        
        [ObservableProperty]
        private RailFactory.Core.ScanOptions _scanOptions = new RailFactory.Core.ScanOptions();

        public SettingsViewModel()
        {
            _settingsService = new SettingsService();
            _dialogService = new DialogService();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = _settingsService.LoadSettings();
            OutputPath = settings.OutputPath;
            ScanOptions = settings.ScanOptions ?? new RailFactory.Core.ScanOptions();
        }

        [RelayCommand]
        private void Save()
        {
            var currentSettings = _settingsService.LoadSettings();
            
            var settings = new AppSettings
            {
                RuntimePath = currentSettings.RuntimePath,
                RailEnginePath = currentSettings.RailEnginePath,
                OutputPath = OutputPath,
                ScanOptions = ScanOptions
            };
            _settingsService.SaveSettings(settings);
        }

        [RelayCommand]
        private void BrowseOutput()
        {
            var path = _dialogService.OpenFolder();
            if (!string.IsNullOrEmpty(path))
            {
                OutputPath = path;
            }
        }
    }
}




