using System;
using System.IO;
using System.Text.Json;
using RailStudio.Models;

namespace RailStudio.Services
{
    public interface ISettingsService
    {
        AppSettings LoadSettings();
        void SaveSettings(AppSettings settings);
    }

    public class SettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;

        public SettingsService()
        {
            _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        }

        public AppSettings LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                // Default Configuration
                return new AppSettings
                {
                    RuntimePath = string.Empty, // Not needed - using .exe
                    RailEnginePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "RailSDK", "builder", "Rail-builder.exe"),
                    OutputPath = string.Empty
                };
            }

            try
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

                // Fallback for missing values (migration or empty file)
                if (string.IsNullOrEmpty(settings.RuntimePath))
                    settings.RuntimePath = string.Empty; // Not needed
                
                if (string.IsNullOrEmpty(settings.RailEnginePath))
                    settings.RailEnginePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "RailSDK", "builder", "Rail-builder.exe");
                
                // OutputPath is preserved from JSON - no override needed

                return settings;
            }
            catch
            {
                return new AppSettings
                {
                    RuntimePath = string.Empty,
                    RailEnginePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "RailSDK", "builder", "Rail-builder.exe"),
                    OutputPath = string.Empty
                };
            }
        }

        public void SaveSettings(AppSettings settings)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_settingsFilePath, json);
        }
    }
}




