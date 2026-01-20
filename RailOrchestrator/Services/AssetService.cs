using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using RailFactory.Core;

namespace WpfRagApp.Services
{
    /// <summary>
    /// Type of asset - determines behavior and UI styling.
    /// </summary>
    public enum AssetType
    {
        /// <summary>Local executable with RailEngine</summary>
        Exe,
        /// <summary>External web API via HTTP</summary>
        Api
    }

    /// <summary>
    /// Represents a Rail asset (EXE or API).
    /// 
    /// ENTERPRISE DESIGN:
    /// - Unified model for both EXE and API assets
    /// - Type-based UI styling (colors, icons)
    /// - INotifyPropertyChanged for WPF binding
    /// </summary>
    public class AssetInfo : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// Display name (folder name).
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Full path to the asset directory.
        /// </summary>
        public string Path { get; set; } = string.Empty;
        
        /// <summary>
        /// Asset type: Exe or Api.
        /// </summary>
        public AssetType Type { get; set; } = AssetType.Exe;
        
        /// <summary>
        /// Icon for display (📦 for exe, 🌐 for api).
        /// </summary>
        public string Icon => Type == AssetType.Api ? "🌐" : "📦";
        
        /// <summary>
        /// True if this is a composite (multi-module) manifest.
        /// </summary>
        public bool IsComposite { get; set; }
        
        /// <summary>
        /// Number of modules in composite manifest.
        /// </summary>
        public int ModuleCount { get; set; }
        
        /// <summary>
        /// Total number of tools/skills.
        /// </summary>
        public int ToolCount { get; set; }
        
        /// <summary>
        /// Whether this asset is currently selected.
        /// </summary>
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
        
        /// <summary>
        /// Chip border color based on type.
        /// </summary>
        public string ChipBorderColor => Type == AssetType.Api ? "#3A6B5C" : "#444444";
        
        /// <summary>
        /// Chip accent color based on type.
        /// </summary>
        public string ChipAccentColor => Type == AssetType.Api ? "#5E9B8A" : "#007ACC";
        
        /// <summary>
        /// Chip selected glow color.
        /// </summary>
        public string ChipGlowColor => Type == AssetType.Api ? "#5E9B8A" : "#00D9FF";
        
        /// <summary>
        /// Internal name defined inside the manifest (e.g. Assembly Name).
        /// Used for linking running processes to assets.
        /// </summary>
        public string InternalName { get; set; } = string.Empty;

        /// <summary>
        /// Display subtitle for UI.
        /// </summary>
        public string Subtitle
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                    return "No tools";
                
                var typeLabel = Type == AssetType.Api ? "skills" : "tools";
                    
                if (IsComposite)
                    return $"{ModuleCount} modules • {ToolCount} {typeLabel}";
                    
                return ToolCount > 0 ? $"{ToolCount} {typeLabel}" : "";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Service for discovering and managing unified assets (EXE + API).
    /// 
    /// ENTERPRISE DESIGN:
    /// - Single folder scanning for both types
    /// - Manifest detection: Rail.manifest.json = EXE, api.manifest.json = API
    /// - Unified metadata extraction
    /// - Configurable root path via SettingsService
    /// </summary>
    public class AssetService
    {
        private readonly SettingsService? _settingsService;
        private const string ExeManifestFile = "Rail.manifest.json";
        private const string ApiManifestFile = "api.manifest.json";
        
        // Default path for backward compatibility when no SettingsService is injected
        private const string DefaultRootPath = @"C:\";
        
        /// <summary>
        /// Static accessor for default root path (for legacy code that cannot use DI).
        /// Prefer using instance property AssetsRootPath when possible.
        /// </summary>
        public static string GetDefaultRootPath() => DefaultRootPath;
        
        /// <summary>
        /// Gets the root path for all assets (from settings or default).
        /// </summary>
        public string AssetsRootPath => _settingsService?.AssetsRootPath ?? DefaultRootPath;

        /// <summary>
        /// Default constructor for backward compatibility.
        /// Uses default hardcoded path.
        /// </summary>
        public AssetService()
        {
            _settingsService = null;
        }

        /// <summary>
        /// Constructor with SettingsService injection for configurable path.
        /// </summary>
        public AssetService(SettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        /// <summary>
        /// Scans for all valid assets (EXE and API).
        /// </summary>
        public List<AssetInfo> GetAssets()
        {
            var assets = new List<AssetInfo>();
            var rootPath = AssetsRootPath;

            if (!Directory.Exists(rootPath))
            {
                System.Diagnostics.Debug.WriteLine($"[AssetService] Root path not found: {rootPath}");
                return assets;
            }

            // Check root directory
            if (IsValidAsset(rootPath))
            {
                assets.Add(CreateAssetInfo(rootPath));
            }

            // Scan subdirectories (only first level for cleaner UX)
            try
            {
                var subDirs = Directory.GetDirectories(rootPath);
                foreach (var dir in subDirs)
                {
                    if (IsValidAsset(dir))
                    {
                        assets.Add(CreateAssetInfo(dir));
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetService] Error scanning: {ex.Message}");
            }

            // Sort: EXE first, then API
            assets = assets.OrderBy(a => a.Type).ThenBy(a => a.Name).ToList();
            
            System.Diagnostics.Debug.WriteLine($"[AssetService] Found {assets.Count} assets " +
                $"({assets.Count(a => a.Type == AssetType.Exe)} exe, {assets.Count(a => a.Type == AssetType.Api)} api)");
            
            return assets;
        }

        /// <summary>
        /// Checks if a directory contains a valid asset manifest.
        /// </summary>
        private bool IsValidAsset(string path)
        {
            // EXE asset: has Rail.manifest.json or Rail.model.json
            if (File.Exists(System.IO.Path.Combine(path, ExeManifestFile)) ||
                File.Exists(System.IO.Path.Combine(path, "Rail.model.json")))
            {
                return true;
            }
            
            // API asset: must have api.manifest.json
            if (File.Exists(System.IO.Path.Combine(path, ApiManifestFile)))
            {
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Detects asset type based on manifest presence.
        /// </summary>
        private AssetType DetectAssetType(string dir)
        {
            if (File.Exists(System.IO.Path.Combine(dir, ExeManifestFile)) ||
                File.Exists(System.IO.Path.Combine(dir, "Rail.model.json")))
            {
                return AssetType.Exe;
            }
            return AssetType.Api;
        }

        /// <summary>
        /// Creates an AssetInfo with manifest metadata.
        /// </summary>
        private AssetInfo CreateAssetInfo(string dir)
        {
            var assetType = DetectAssetType(dir);
            
            var info = new AssetInfo
            {
                Name = new DirectoryInfo(dir).Name,
                Path = dir,
                Type = assetType,
                IsComposite = false,
                ModuleCount = 0,
                ToolCount = 0,
                InternalName = "" // Default
            };

            if (assetType == AssetType.Exe)
            {
                ParseExeManifest(info, dir);
            }
            else
            {
                ParseApiManifest(info, dir);
            }

            return info;
        }

        private void ParseExeManifest(AssetInfo info, string dir)
        {
            var manifestPath = System.IO.Path.Combine(dir, ExeManifestFile);
            if (!File.Exists(manifestPath)) return;

            try
            {
                var json = File.ReadAllText(manifestPath);
                
                bool hasModules = json.Contains("\"modules\"");
                bool hasManifestType = json.Contains("\"manifest_type\"");

                if (hasModules || hasManifestType)
                {
                    info.IsComposite = true;
                    var manifest = JsonSerializer.Deserialize<CompositeManifest>(json);
                    if (manifest?.Modules != null)
                    {
                        info.ModuleCount = manifest.Modules.Count;
                        info.ToolCount = manifest.Modules.Sum(m => m.Tools?.Count ?? 0);
                    }
                    // Try to get top-level name if available, often composite manifests have a name too
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("name", out var nameEl))
                        info.InternalName = nameEl.ToString();
                }
                else
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("tools", out var toolsElement) &&
                        toolsElement.ValueKind == JsonValueKind.Array)
                    {
                        info.ToolCount = toolsElement.GetArrayLength();
                    }
                    
                    // Parse Internal Name for Linkage
                    if (doc.RootElement.TryGetProperty("name", out var nameEl))
                    {
                        info.InternalName = nameEl.ToString();
                    }
                    else if (doc.RootElement.TryGetProperty("entry_point", out var epEl))
                    {
                        // Fallback: use exe name from entry_point
                        info.InternalName = System.IO.Path.GetFileNameWithoutExtension(epEl.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetService] Error parsing EXE manifest: {ex.Message}");
            }
        }

        private void ParseApiManifest(AssetInfo info, string dir)
        {
            var manifestPath = System.IO.Path.Combine(dir, ApiManifestFile);
            if (!File.Exists(manifestPath)) return;

            try
            {
                var json = File.ReadAllText(manifestPath);
                using var doc = JsonDocument.Parse(json);
                
                // Get display name from provider.name
                if (doc.RootElement.TryGetProperty("provider", out var provider))
                {
                    if (provider.TryGetProperty("name", out var nameElement))
                    {
                        var displayName = nameElement.GetString();
                        if (!string.IsNullOrWhiteSpace(displayName))
                        {
                            info.Name = displayName;
                        }
                    }
                }
                
                // Count skill files
                if (doc.RootElement.TryGetProperty("skillFiles", out var skillFiles) &&
                    skillFiles.ValueKind == JsonValueKind.Array)
                {
                    info.ToolCount = skillFiles.GetArrayLength();
                }
                
                // Or count from skills folder
                var skillsDir = System.IO.Path.Combine(dir, "skills");
                if (Directory.Exists(skillsDir))
                {
                    var skillCount = Directory.GetFiles(skillsDir, "*.json").Length;
                    if (skillCount > info.ToolCount) info.ToolCount = skillCount;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AssetService] Error parsing API manifest: {ex.Message}");
            }
        }
    }
}





