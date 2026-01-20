using System.IO;
using System.Windows;
using System.Windows.Input;
using WpfRagApp.Services;
using Microsoft.Win32;

namespace WpfRagApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private string _geminiApiKey;
        private string _openAIApiKey;
        private string _anthropicApiKey;
        private string _assetsRootPath;

        public SettingsViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _geminiApiKey = _settingsService.GeminiApiKey;
            _openAIApiKey = _settingsService.OpenAIApiKey;
            _anthropicApiKey = _settingsService.AnthropicApiKey;
            _assetsRootPath = _settingsService.AssetsRootPath;
            
            SaveCommand = new RelayCommand(Save);
            BrowseAssetsPathCommand = new RelayCommand(BrowseAssetsPath);
        }

        public string GeminiApiKey
        {
            get => _geminiApiKey;
            set => SetProperty(ref _geminiApiKey, value);
        }

        public string OpenAIApiKey
        {
            get => _openAIApiKey;
            set => SetProperty(ref _openAIApiKey, value);
        }

        public string AnthropicApiKey
        {
            get => _anthropicApiKey;
            set => SetProperty(ref _anthropicApiKey, value);
        }

        public string AssetsRootPath
        {
            get => _assetsRootPath;
            set
            {
                if (SetProperty(ref _assetsRootPath, value))
                {
                    OnPropertyChanged(nameof(IsAssetsPathValid));
                    OnPropertyChanged(nameof(AssetsPathStatusIcon));
                }
            }
        }

        /// <summary>
        /// Returns true if the configured path exists.
        /// </summary>
        public bool IsAssetsPathValid => Directory.Exists(_assetsRootPath);

        /// <summary>
        /// Visual indicator for path validity.
        /// </summary>
        public string AssetsPathStatusIcon => IsAssetsPathValid ? "✓" : "✗";

        public ICommand SaveCommand { get; }
        public ICommand BrowseAssetsPathCommand { get; }

        private void BrowseAssetsPath(object? parameter)
        {
            // Use WinForms FolderBrowserDialog for folder selection
            using var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Asset Library Folder",
                ShowNewFolderButton = true,
                SelectedPath = Directory.Exists(_assetsRootPath) ? _assetsRootPath : ""
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                AssetsRootPath = dialog.SelectedPath;
            }
        }

        private void Save(object? parameter)
        {
            _settingsService.GeminiApiKey = GeminiApiKey;
            _settingsService.OpenAIApiKey = OpenAIApiKey;
            _settingsService.AnthropicApiKey = AnthropicApiKey;
            _settingsService.AssetsRootPath = AssetsRootPath;
            MessageBox.Show("Settings saved successfully!", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}





