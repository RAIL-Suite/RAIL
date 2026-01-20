using System;
using System.IO;
using System.Text.Json;

namespace WpfRagApp.Services
{
    public class AppSettings
    {
        public string GeminiApiKey { get; set; } = string.Empty;
        public string OpenAIApiKey { get; set; } = string.Empty;
        public string AnthropicApiKey { get; set; } = string.Empty;
        public string SelectedModelId { get; set; } = "gemini-2.5-flash";
        public double Temperature { get; set; } = 0.2;
        public bool ReActEnabled { get; set; } = true;
        public int ReActMaxSteps { get; set; } = 10;
        public string AssetsRootPath { get; set; } = string.Empty;
    }

    public class SettingsService
    {
        private readonly string _settingsFilePath;
        private AppSettings _currentSettings;

        /// <summary>
        /// Fired when any setting changes. Parameter is the property name.
        /// </summary>
        public event EventHandler<string>? SettingChanged;

        private void OnSettingChanged(string propertyName)
        {
            SettingChanged?.Invoke(this, propertyName);
        }

        public SettingsService()
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WpfRagApp");
            Directory.CreateDirectory(appDataPath);
            _settingsFilePath = Path.Combine(appDataPath, "user_settings.json");
            _currentSettings = LoadSettings();
        }

        public string GeminiApiKey
        {
            get => _currentSettings.GeminiApiKey;
            set
            {
                _currentSettings.GeminiApiKey = value;
                SaveSettings();
            }
        }

        public string OpenAIApiKey
        {
            get => _currentSettings.OpenAIApiKey;
            set
            {
                _currentSettings.OpenAIApiKey = value;
                SaveSettings();
            }
        }

        public string AnthropicApiKey
        {
            get => _currentSettings.AnthropicApiKey;
            set
            {
                _currentSettings.AnthropicApiKey = value;
                SaveSettings();
            }
        }

        public string SelectedModelId
        {
            get => _currentSettings.SelectedModelId;
            set
            {
                _currentSettings.SelectedModelId = value;
                SaveSettings();
            }
        }

        public double Temperature
        {
            get => _currentSettings.Temperature;
            set
            {
                _currentSettings.Temperature = value;
                SaveSettings();
            }
        }

        public bool ReActEnabled
        {
            get => _currentSettings.ReActEnabled;
            set
            {
                _currentSettings.ReActEnabled = value;
                SaveSettings();
            }
        }

        public int ReActMaxSteps
        {
            get => _currentSettings.ReActMaxSteps;
            set
            {
                _currentSettings.ReActMaxSteps = value;
                SaveSettings();
            }
        }

        public string AssetsRootPath
        {
            get => _currentSettings.AssetsRootPath;
            set
            {
                _currentSettings.AssetsRootPath = value;
                SaveSettings();
                OnSettingChanged(nameof(AssetsRootPath));
            }
        }

        private AppSettings LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    return new AppSettings();
                }
            }
            return new AppSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch (Exception ex)
            {
                // Handle or log error
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }
    }
}





