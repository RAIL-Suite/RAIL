using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RailStudio.Models;
using RailStudio.Services;
using RailFactory.Core;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace RailStudio.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISettingsService _settingsService;
        private readonly IBuilderService _builderService;
        private readonly IManifestService _manifestService;
        private readonly IDialogService _dialogService;
        private readonly FileSystemService _fileSystemService;
        private readonly IBuildRegistry _buildRegistry;
        private readonly IManifestBackupService _backupService;

        [ObservableProperty]
        private string _outputLog = "> Rail Studio Initialized...\n> Ready.";

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BuildCommand))]
        private bool _isBuilding;

        [ObservableProperty]
        private ObservableCollection<RailTool> _packages = new();

        [ObservableProperty]
        private ObservableCollection<ToolFunctionModel> _functionModels = new();

        [ObservableProperty]
        private int _totalFunctionCount;

        [ObservableProperty]
        private ObservableCollection<FileSystemNode> _projectRoot = new();
        
        [ObservableProperty]
        private ObservableCollection<FileSystemNode> _assetsRoot = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BuildCommand))]
        private string _selectedFilePath = string.Empty;

        [ObservableProperty]
        private string _projectName = "No Project Open";

        [ObservableProperty]
        private int _selectedTabIndex;

        private string _lastArtifactPath = string.Empty;
        
        [ObservableProperty]
        private string _activeAssetPath = string.Empty;
        
        private FileSystemWatcher? _assetsWatcher;

        public MainViewModel()
        {
            _settingsService = new SettingsService();
            _builderService = new BuilderService();
            _manifestService = new ManifestService();
            _dialogService = new DialogService();
            _fileSystemService = new FileSystemService();
            _buildRegistry = new BuildRegistry();
            _backupService = new ManifestBackupService();
            
            LoadPreviewFromOutputFolder();
            LoadAssetsFromOutputFolder();
            InitializeAssetsWatcher();
        }
        
        private void InitializeAssetsWatcher()
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                if (string.IsNullOrEmpty(settings.OutputPath) || !Directory.Exists(settings.OutputPath))
                    return;
                
                _assetsWatcher = new FileSystemWatcher(settings.OutputPath)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };
                
                _assetsWatcher.Created += (s, e) => Application.Current.Dispatcher.Invoke(() => LoadAssetsFromOutputFolder());
                _assetsWatcher.Deleted += (s, e) => Application.Current.Dispatcher.Invoke(() => LoadAssetsFromOutputFolder());
                _assetsWatcher.Renamed += (s, e) => Application.Current.Dispatcher.Invoke(() => LoadAssetsFromOutputFolder());
                _assetsWatcher.Changed += (s, e) => Application.Current.Dispatcher.Invoke(() => LoadAssetsFromOutputFolder());
                
                AppendLog("> Assets auto-refresh enabled");
            }
            catch (Exception ex)
            {
                AppendLog($"> Warning: Could not initialize assets watcher: {ex.Message}");
            }
        }

        [RelayCommand]
        private void OpenProject()
        {
            var folder = _dialogService.OpenFolder();
            if (!string.IsNullOrEmpty(folder))
            {
                LoadProject(folder);
            }
        }

        [RelayCommand]
        private void OpenSettings()
        {
            var settingsWindow = new Views.SettingsWindow
            {
                Owner = Application.Current.MainWindow
            };
            settingsWindow.ShowDialog();
            
            _assetsWatcher?.Dispose();
            InitializeAssetsWatcher();
            LoadAssetsFromOutputFolder();
        }
        
        private void LoadProject(string folderPath)
        {
            try
            {
                AppendLog($"> Loading project: {folderPath}");
                
                var rootNode = _fileSystemService.LoadDirectory(folderPath);
                
                ProjectRoot.Clear();
                ProjectRoot.Add(rootNode);
                
                ProjectName = System.IO.Path.GetFileName(folderPath) ?? "Project";
                AppendLog($"> Loaded {CountFiles(rootNode)} files");
                
                LoadPreviewFromOutputFolder();
            }
            catch (Exception ex)
            {
                AppendLog($"> ERROR loading project: {ex.Message}");
            }
        }
        
        private int CountFiles(FileSystemNode node)
        {
            int count = node.IsDirectory ? 0 : 1;
            foreach (var child in node.Children)
            {
                count += CountFiles(child);
            }
            return count;
        }
        
        private void LoadPreviewFromOutputFolder()
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                var outputFolder = settings.OutputPath;
                
                if (string.IsNullOrEmpty(outputFolder) || !Directory.Exists(outputFolder))
                {
                    AppendLog("> Preview: Output folder not configured or doesn't exist");
                    return;
                }
                
                var manifestPath = Path.Combine(outputFolder, Constants.Rail_MANIFEST_FILENAME);
                if (!File.Exists(manifestPath))
                {
                    AppendLog($"> Preview: {Constants.Rail_MANIFEST_FILENAME} not found in output folder");
                    Packages.Clear();
                    return;
                }
                
                
                Task.Run(async () =>
                {
                    try
                    {
                        var jsonText = File.ReadAllText(manifestPath);
                        
                        AppendLog($"> DEBUG: Checking manifest type...");
                        bool hasModules = jsonText.Contains("\"modules\"");
                        bool hasManifestType = jsonText.Contains("\"manifest_type\"");
                        AppendLog($">   Has 'modules': {hasModules}, Has 'manifest_type': {hasManifestType}");
                        
                        // Detect manifest type by checking for composite-specific properties
                        if (hasModules || hasManifestType)
                        {
                            AppendLog($"> DEBUG: Detected COMPOSITE manifest");
                            // Load composite manifest (Build Solution output)
                            var compositeManifest = System.Text.Json.JsonSerializer.Deserialize<RailFactory.Core.CompositeManifest>(jsonText);
                            
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ActiveAssetPath = manifestPath;
                                Packages.Clear();
                                
                                if (compositeManifest?.Modules != null)
                                {
                                    int totalTools = 0;
                                    
                                    // Extract tools from all modules
                                    foreach (var module in compositeManifest.Modules)
                                    {
                                        if (module.Tools != null)
                                        {
                                            foreach (var tool in module.Tools)
                                            {
                                                // Convert ToolDefinition to RailTool
                                                var RailTool = new RailTool
                                                {
                                                    Name = tool.Name,
                                                    Description = tool.Description,
                                                    InputSchema = tool.Parameters
                                                };
                                                Packages.Add(RailTool);
                                                totalTools++;
                                            }
                                        }
                                    }
                                    
                                    AppendLog($"> Preview: Loaded {totalTools} tools from {compositeManifest.Modules.Count} modules ({Path.GetFileName(manifestPath)})");
                                    LoadFunctionModels();
                                    
                                    // Populate Assembly and Type metadata
                                    PopulateCompositeMetadata(compositeManifest);
                                }
                            });
                        }
                        else
                        {
                            AppendLog($"> DEBUG: Detected SINGLE manifest");
                            // Load single manifest (Build single file output)
                            var manifest = await _manifestService.LoadManifestAsync(manifestPath);
                            
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ActiveAssetPath = manifestPath;
                                Packages.Clear();
                                if (manifest != null && manifest.Tools != null)
                                {
                                    foreach (var tool in manifest.Tools)
                                    {
                                        Packages.Add(tool);
                                    }
                                    AppendLog($"> Preview: Loaded {manifest.Tools.Count} tools from {Path.GetFileName(manifestPath)}");
                                    LoadFunctionModels();
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            AppendLog($"> Preview ERROR: {ex.Message}");
                            AppendLog($">   Stack: {ex.StackTrace}");
                            if (ex.InnerException != null)
                            {
                                AppendLog($">   Inner: {ex.InnerException.Message}");
                            }
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                AppendLog($"> Preview ERROR: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ImportBinary()
        {
            var filePath = _dialogService.OpenFile("Select Binary File|*.exe;*.dll;*.jar|All Files|*.*");
            if (string.IsNullOrEmpty(filePath))
                return;

            try
            {
                AppendLog($"> Detecting binary type: {Path.GetFileName(filePath)}");

                var runtimeType = RailFactory.Core.RuntimeRegistry.DetectRuntime(filePath);

                if (runtimeType == RailFactory.Core.RuntimeType.Unknown)
                {
                    AppendLog($"> ERROR: Unsupported binary format");
                    _dialogService.ShowMessage("Unsupported Binary", 
                        "Could not detect binary type. Only .NET assemblies (.exe/.dll) are currently supported.");
                    return;
                }

                AppendLog($"> Detected: {runtimeType}");
                
                var managedPath = RailFactory.Core.RuntimeRegistry.GetManagedAssemblyPath(filePath);
                if (managedPath != filePath)
                {
                    AppendLog($"> Native apphost detected - using managed DLL: {Path.GetFileName(managedPath)}");
                }
                
                AppendLog($"> Scanning methods...");

                var scanner = RailFactory.Core.RuntimeRegistry.GetScanner(runtimeType);
                
                // Load scan options from settings
                var settings = _settingsService.LoadSettings();
                var scanOptions = settings.ScanOptions ?? new RailFactory.Core.ScanOptions();
                
                var methods = scanner.ScanBinary(managedPath, scanOptions);

                AppendLog($"> Found {methods.Count} public methods");

                var outputSettings = _settingsService.LoadSettings();
                var binaryName = Path.GetFileNameWithoutExtension(filePath);
                var outputDir = Path.Combine(outputSettings.OutputPath, $"{binaryName}_binary");
                
                Directory.CreateDirectory(outputDir);
                
                var manifest = new
                {
                    name = binaryName,
                    version = "1.0.0",
                    runtime_type = ConvertToStandardRuntimeString(runtimeType),
                    entry_point = filePath,
                    description = $"Binary runtime control for {binaryName}",
                    tools = methods.Select(m => new
                    {
                        name = m.MethodName,
                        @class = m.ClassName,
                        description = m.Description,
                        parameters = new
                        {
                            type = "OBJECT",
                            properties = m.Parameters.ToDictionary(
                                p => p.Name,
                                p => new
                                {
                                    type = ConvertTypeToGemini(p.ParameterType),
                                    description = $"{p.ParameterType.Name} parameter"
                                }
                            ),
                            required = m.Parameters.Where(p => !p.IsOptional).Select(p => p.Name).ToArray()
                        }
                    }).ToArray()
                };

                var manifestPath = Path.Combine(outputDir, Constants.Rail_MANIFEST_FILENAME);
                var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });
                
                File.WriteAllText(manifestPath, manifestJson);
                
                AppendLog($"> Manifest created: {manifestPath}");
                AppendLog($"> Binary artifact ready at: {outputDir}");
                AppendLog($"> Import complete!");
            }
            catch (Exception ex)
            {
                AppendLog($"> ERROR: {ex.Message}");
                _dialogService.ShowMessage("Import Failed", ex.Message);
            }
        }

        private string ConvertTypeToGemini(Type type)
        {
            if (type == typeof(string)) return "STRING";
            if (type == typeof(int) || type == typeof(long)) return "INTEGER";
            if (type == typeof(double) || type == typeof(float)) return "NUMBER";
            if (type == typeof(bool)) return "BOOLEAN";
            if (type.IsArray) return "ARRAY";
            return "OBJECT";
        }

        [RelayCommand(CanExecute = nameof(CanBuild))]
        public async Task Build()
        {
            if (string.IsNullOrEmpty(SelectedFilePath))
            {
                AppendLog("> ERROR: No file selected");
                return;
            }

            IsBuilding = true;
            AppendLog("--- Build Started ---");

            try
            {
                var runtimeType = _buildRegistry.GetRuntimeType(SelectedFilePath);
                
                if (runtimeType == "binary")
                {
                    await BuildBinaryProject();
                    LoadPreviewFromOutputFolder();
                }
                else if (runtimeType == "unknown")
                {
                     AppendLog($"> ERROR: Unsupported file type: {Path.GetExtension(SelectedFilePath)}");
                }
                else
                {
                    var settings = _settingsService.LoadSettings();
                    if (string.IsNullOrEmpty(settings.OutputPath))
                    {
                        AppendLog("> ERROR: Output path not configured in settings");
                        return;
                    }
                    
                    var buildName = Path.GetFileNameWithoutExtension(SelectedFilePath);
                    
                    // Show dialog to get custom folder name (CONSISTENT with binary builds)
                    var dialog = new Views.OutputFolderDialog(settings.OutputPath, buildName);
                    if (dialog.ShowDialog() != true)
                    {
                        AppendLog("> Build cancelled by user");
                        return;
                    }
                    
                    // Create output folder
                    _lastArtifactPath = Path.Combine(settings.OutputPath, dialog.FolderName);
                    Directory.CreateDirectory(_lastArtifactPath);

                    AppendLog($"> Building: {buildName}");
                    AppendLog($"> Runtime: {runtimeType}");
                    AppendLog($"> Output: {_lastArtifactPath}");

                    var runtimePath = settings.RuntimePath;
                    var enginePath = settings.RailEnginePath;

                    // Pass output folder and name to builder
                    string args = $"build \"{SelectedFilePath}\" --output \"{settings.OutputPath}\" --name \"{dialog.FolderName}\"";
                    
                    if (runtimeType != "python")
                    {
                        args += $" --runtime {runtimeType}";
                    }

                    await _builderService.RunBuilderAsync(runtimePath, enginePath, args, OnOutputReceived);
                    
                    if (!string.IsNullOrEmpty(_lastArtifactPath))
                    {
                        // Enterprise Fix: Enforce Rail.manifest.json naming
                        // The Python builder generates 'manifest.json', so we rename it
                        var legacyManifestPath = Path.Combine(_lastArtifactPath, "manifest.json");
                        var correctManifestPath = Path.Combine(_lastArtifactPath, Constants.Rail_MANIFEST_FILENAME);
                        
                        if (File.Exists(legacyManifestPath) && !File.Exists(correctManifestPath))
                        {
                            File.Move(legacyManifestPath, correctManifestPath);
                            AppendLog($"> Renamed manifest.json to {Constants.Rail_MANIFEST_FILENAME}");
                        }

                        // Update UI to show new asset in green
                        ActiveAssetPath = _lastArtifactPath;
                        LoadAssetsFromOutputFolder();

                        if (File.Exists(correctManifestPath))
                        {
                            var manifest = await _manifestService.LoadManifestAsync(correctManifestPath);
                            
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Packages.Clear();
                                if (manifest?.Tools != null)
                                {
                                    foreach (var tool in manifest.Tools)
                                    {
                                        Packages.Add(tool);
                                    }
                                }
                                SelectedTabIndex = 0;
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog($"> BUILD ERROR: {ex.Message}");
            }

            AppendLog("--- Build Finished ---");
            IsBuilding = false;
        }

        private bool CanBuild()
        {
            return !IsBuilding && _buildRegistry.CanBuild(SelectedFilePath);
        }

        private void OnOutputReceived(string output)
        {
            Application.Current.Dispatcher.Invoke(() => AppendLog(output));
        }

        [RelayCommand]
        public void SelectFile(FileSystemNode node)
        {
            if (!node.IsDirectory)
            {
                SelectedFilePath = node.Path;
                AppendLog($"> Selected: {node.Name} ({_buildRegistry.GetRuntimeType(node.Path)})");
            }
        }

        private void AppendLog(string message)
        {
            OutputLog += $"{message}\n";
        }
        
        private async Task BuildBinaryProject()
        {
            var filePath = SelectedFilePath;
            
            try
            {
                AppendLog($"> Detecting runtime: {Path.GetFileName(filePath)}");
                var runtimeType = RailFactory.Core.RuntimeRegistry.DetectRuntime(filePath);
                
                if (runtimeType == RailFactory.Core.RuntimeType.Unknown)
                {
                    AppendLog("> ERROR: Unsupported binary type");
                    return;
                }
                
                AppendLog($"> Runtime: {runtimeType}");
                var managedPath = RailFactory.Core.RuntimeRegistry.GetManagedAssemblyPath(filePath);
                
                var scanner = RailFactory.Core.RuntimeRegistry.GetScanner(runtimeType);
                
                // Load scan options from settings
                var settings = _settingsService.LoadSettings();
                var scanOptions = settings.ScanOptions ?? new RailFactory.Core.ScanOptions();
                
                var methods = scanner.ScanBinary(managedPath, scanOptions);
                
                AppendLog($"> Found: {methods.Count} methods");
                
                var buildSettings = _settingsService.LoadSettings();
                if (string.IsNullOrEmpty(buildSettings.OutputPath))
                {
                    AppendLog("> ERROR: Output path not configured in settings");
                    return;
                }
                
                var binaryName = Path.GetFileNameWithoutExtension(filePath);
                
                // Show dialog to get custom folder name
                var dialog = new Views.OutputFolderDialog(buildSettings.OutputPath, binaryName);
                if (dialog.ShowDialog() != true)
                {
                    AppendLog("> Build cancelled by user");
                    return;
                }
                
                var outputDir = Path.Combine(buildSettings.OutputPath, dialog.FolderName);
                Directory.CreateDirectory(outputDir);
                
                AppendLog($"> Output folder: {dialog.FolderName}");
                
                var manifest = new
                {
                    name = binaryName,
                    version = "1.0.0",
                    runtime_type = ConvertToStandardRuntimeString(runtimeType),
                    language = runtimeType.ToLanguageString(),
                    entry_point = managedPath,
                    description = $"Scanned from {binaryName}",
                    tools = methods.Select(m => new
                    {
                        name = m.MethodName,
                        @class = m.ClassName,
                        description = m.Description,
                        parameters = new
                        {
                            type = "OBJECT",
                            properties = m.Parameters.ToDictionary(
                                p => p.Name,
                                p => p.ParameterSchema ?? new Dictionary<string, object>
                                {
                                    { "type", ConvertTypeToGemini(p.ParameterType) },
                                    { "description", p.ParameterType.Name }
                                }
                            ),
                            required = m.Parameters.Where(p => !p.IsOptional).Select(p => p.Name).ToArray()
                        }
                    }).ToArray()
                };

                var manifestPath = Path.Combine(outputDir, Constants.Rail_MANIFEST_FILENAME);
                var manifestJson = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

                File.WriteAllText(manifestPath, manifestJson);
                AppendLog($"> Manifest saved: {Constants.Rail_MANIFEST_FILENAME}");
                _lastArtifactPath = outputDir;
                ActiveAssetPath = outputDir;
            }
            catch (Exception ex)
            {
                AppendLog($"> ERROR: {ex.Message}");
                throw;
            }
        }
        
        private string CreateVersionedOutputFolder(string baseName, string outputPath)
        {
            var baseFolder = Path.Combine(outputPath, baseName);
            
            if (!Directory.Exists(baseFolder))
            {
                Directory.CreateDirectory(baseFolder);
                return baseFolder;
            }
            
            int version = 2;
            string versionedFolder;
            do
            {
                versionedFolder = Path.Combine(outputPath, $"{baseName}_v{version}");
                version++;
            }
            while (Directory.Exists(versionedFolder));
            
            Directory.CreateDirectory(versionedFolder);
            return versionedFolder;
        }
        
        public async void LoadPreviewFromAsset(string manifestPath)
        {
            try
            {
                if (!File.Exists(manifestPath))
                {
                    AppendLog($"> Asset manifest not found: {manifestPath}");
                    return;
                }
                
                var jsonText = File.ReadAllText(manifestPath);
                
                // Detect manifest type by checking for composite-specific properties
                bool hasModules = jsonText.Contains("\"modules\"");
                bool hasManifestType = jsonText.Contains("\"manifest_type\"");
                
                if (hasModules || hasManifestType)
                {
                    // Load composite manifest (Build Solution output)
                    var compositeManifest = System.Text.Json.JsonSerializer.Deserialize<RailFactory.Core.CompositeManifest>(jsonText);
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ActiveAssetPath = manifestPath;
                        Packages.Clear();
                        
                        if (compositeManifest?.Modules != null)
                        {
                            int totalTools = 0;
                            
                            foreach (var module in compositeManifest.Modules)
                            {
                                if (module.Tools != null)
                                {
                                    foreach (var tool in module.Tools)
                                    {
                                        var RailTool = new RailTool
                                        {
                                            Name = tool.Name,
                                            Description = tool.Description,
                                            ClassName = tool.ClassName,
                                            InputSchema = tool.Parameters
                                        };
                                        Packages.Add(RailTool);
                                        totalTools++;
                                    }
                                }
                            }
                            
                            AppendLog($"> Loaded {totalTools} tools from {compositeManifest.Modules.Count} modules ({Path.GetFileName(manifestPath)})");
                            LoadFunctionModels();
                            PopulateCompositeMetadata(compositeManifest);
                        }
                        SelectedTabIndex = 0;
                    });
                }
                else
                {
                    // Load single manifest (Build single file output)
                    var manifest = await _manifestService.LoadManifestAsync(manifestPath);
                    
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ActiveAssetPath = manifestPath;
                        Packages.Clear();
                        if (manifest?.Tools != null)
                        {
                            foreach (var tool in manifest.Tools)
                            {
                                Packages.Add(tool);
                            }
                            AppendLog($"> Loaded {manifest.Tools.Count} tools from {Path.GetFileName(manifestPath)}");
                            LoadFunctionModels();
                        }
                        SelectedTabIndex = 0;
                    });
                }
            }
            catch (Exception ex)
            {
                AppendLog($"> ERROR loading asset: {ex.Message}");
            }
        }
        
        private void LoadAssetsFromOutputFolder()
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                if (string.IsNullOrEmpty(settings.OutputPath) || !Directory.Exists(settings.OutputPath))
                {
                    AssetsRoot.Clear();
                    return;
                }
                
                var rootNode = _fileSystemService.LoadDirectory(settings.OutputPath);
                
                if (!string.IsNullOrEmpty(ActiveAssetPath))
                {
                    MarkActiveAsset(rootNode, ActiveAssetPath);
                }
                
                AssetsRoot.Clear();
                foreach (var child in rootNode.Children)
                {
                    AssetsRoot.Add(child);
                }
            }
            catch (Exception ex)
            {
                AppendLog($"> Assets ERROR: {ex.Message}");
            }
        }
        
        private bool MarkActiveAsset(FileSystemNode node, string activePath)
        {
            if (node.Path == activePath)
            {
                node.IsActiveAsset = true;
                node.IsSelected = true; // Restore selection
                return true;
            }
            
            bool foundChild = false;
            foreach (var child in node.Children)
            {
                if (MarkActiveAsset(child, activePath))
                {
                    foundChild = true;
                }
            }

            if (foundChild)
            {
                node.IsExpanded = true; // Expand parent if child is active
            }
            
            return foundChild;
        }

        [ObservableProperty]
        private bool _showOverloads;

        /// <summary>
        /// Loads function models from Packages collection and detects duplicates.
        /// </summary>
        private void LoadFunctionModels()
        {
            FunctionModels.Clear();
            
            if (Packages == null || Packages.Count == 0)
            {
                TotalFunctionCount = 0;
                return;
            }

            // Create models with index
            var models = Packages.Select((p, index) => 
            {
                var model = new ToolFunctionModel(p);
                model.Index = index;
                return model;
            }).ToList();

            // Detect duplicates by signature hash
            var signatureGroups = models
                .GroupBy(m => m.GetSignatureHash())
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToHashSet();

            // Detect overloads by Name (same name, potentially different signature)
            var nameGroups = models
                .GroupBy(m => m.Name)
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToHashSet();

            // Mark duplicates and overloads
            foreach (var model in models)
            {
                model.IsDuplicate = signatureGroups.Contains(model);
                model.IsOverload = nameGroups.Contains(model);
                FunctionModels.Add(model);
            }

            TotalFunctionCount = models.Count;
        }

        /// <summary>
        /// Deletes a function from the current manifest after user confirmation.
        /// </summary>
        [RelayCommand]
        private async Task DeleteFunction(ToolFunctionModel? model)
        {
            if (model == null)
            {
                AppendLog("> ERROR: No function selected for deletion");
                return;
            }

            try
            {
                // Get current manifest path
                var settings = _settingsService.LoadSettings();
                var manifestPath = ActiveAssetPath;// Path.Combine(ActiveAssetPath, Constants.Rail_MANIFEST_FILENAME);

                if (!File.Exists(manifestPath))
                {
                    AppendLog("> ERROR: No manifest found in output folder");
                    return;
                }

                // Show confirmation dialog
                var dialog = new Views.DeleteFunctionDialog(model.GetDisplaySummary())
                {
                    Owner = Application.Current.MainWindow
                };

                var confirmed = dialog.ShowDialog() == true;
                if (!confirmed)
                {
                    AppendLog("> Function deletion cancelled");
                    return;
                }

                // Create backup
                AppendLog($"> Creating backup...");
                var backupPath = _backupService.CreateBackup(manifestPath);
                AppendLog($"> Backup created: {Path.GetFileName(backupPath)}");

                // Delete function
                var toolIndex = Packages.IndexOf(model.Tool);
                var success = await _manifestService.DeleteToolAsync(manifestPath, model.Name, toolIndex, model.ClassName);

                if (success)
                {
                    AppendLog($"> Deleted function: {model.Name}");
                    
                    // Reload preview
                    LoadPreviewFromOutputFolder();
                }
                else
                {
                    AppendLog($"> ERROR: Failed to delete function {model.Name}");
                }
            }
            catch (Exception ex)
            {
                AppendLog($"> ERROR deleting function: {ex.Message}");
                _dialogService.ShowMessage("Delete Failed", ex.Message);
            }
        }

        /// <summary>
        /// Shows the batch duplicate manager dialog for duplicates of the selected tool.
        /// </summary>
        [RelayCommand]
        private void ShowDuplicates(ToolFunctionModel? target)
        {
            if (target == null) return;
            
            AppendLog($"> Opening duplicate manager for: {target.Name}...");
            try
            {
                // Use the currently active manifest path
                var manifestPath = ActiveAssetPath;

                if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                {
                    // Fallback to default output path if active path is lost
                    var settings = _settingsService.LoadSettings();
                    manifestPath = Path.Combine(settings.OutputPath, Constants.Rail_MANIFEST_FILENAME);
                    
                    if (!File.Exists(manifestPath))
                    {
                        AppendLog("> ERROR: No manifest found");
                        return;
                    }
                }

                // 1. Identify Target Group (Duplicates or Overloads)
                ObservableCollection<ToolFunctionModel> duplicates;

                if (ShowOverloads && target.IsOverload)
                {
                    // Match by Name (Overloads)
                    duplicates = new ObservableCollection<ToolFunctionModel>(
                        FunctionModels.Where(f => f.Name == target.Name)
                    );
                    AppendLog($"> Managing Overloads: {duplicates.Count} variations of '{target.Name}' found.");
                }
                else
                {
                    // Match by Exact Signature (Duplicates)
                    var targetHash = target.GetSignatureHash();
                    duplicates = new ObservableCollection<ToolFunctionModel>(
                        FunctionModels.Where(f => f.IsDuplicate && f.GetSignatureHash() == targetHash)
                    );
                }

                if (duplicates.Count <= 1 && (!ShowOverloads || duplicates.Count == 0))
                {
                    _dialogService.ShowMessage("No Matches", "No other duplicates or overloads found for this function.");
                    return;
                }

                // 3. Create ViewModel
                var viewModel = new DuplicatesManagerViewModel(
                    _manifestService,
                    _backupService,
                    manifestPath,
                    duplicates
                );

                // 4. Show Dialog
                var dialog = new Views.DuplicatesManagerDialog
                {
                    Owner = Application.Current.MainWindow,
                    DataContext = viewModel
                };

                dialog.ShowDialog();

                // 5. Reload after dialog closes
                if (!string.IsNullOrEmpty(ActiveAssetPath))
                {
                    LoadPreviewFromAsset(ActiveAssetPath);
                }
                else
                {
                    LoadPreviewFromOutputFolder();
                }
            }
            catch (Exception ex)
            {
                AppendLog($"> ERROR showing duplicates: {ex.Message}");
                _dialogService.ShowMessage("Error", ex.Message);
            }
        }
        
        [RelayCommand]
        private async Task BuildSolution()
        {
            if (ProjectRoot.Count == 0 || string.IsNullOrEmpty(ProjectName) || ProjectName == "No Project Open")
            {
                _dialogService.ShowMessage("No Project", "Please open a project folder first.");
                return;
            }
            
            try
            {
                AppendLog("> ===== BUILD SOLUTION (Composite Manifest) =====");
                AppendLog($"> Scanning folder: {ProjectName}");
                
                // Get project root path (first node's path parent)
                var firstNode = ProjectRoot.FirstOrDefault();
                var rootPath = firstNode != null ? Path.GetDirectoryName(firstNode.Path) : "";
                if (string.IsNullOrEmpty(rootPath))
                {
                    AppendLog("> ERROR: Cannot determine project root path");
                    return;
                }

                AppendLog($"> Detecting runtime: {Path.GetFileName(rootPath)}");
                var runtimeType = RailFactory.Core.RuntimeRegistry.DetectRuntime(rootPath);

                if (runtimeType == RailFactory.Core.RuntimeType.Unknown)
                {
                    AppendLog("> ERROR: Unsupported binary type");
                    return;
                }

                // 1. Scan for executables
                var scanner = new RailFactory.Core.SolutionScanner();
                var scanResult = scanner.ScanFolder(rootPath);
                
                AppendLog($"> Found {scanResult.Executables.Count} .NET executable(s)");
                
                // Check if ANY scannable content exists (polyglot support)
                if (!scanResult.HasContent)
                {
                    AppendLog("> ERROR: No scannable content found in folder");
                    _dialogService.ShowMessage("No Content", 
                        "No .NET executables or C++ headers found in the selected folder.\n\n" +
                        "For .NET: Ensure folder contains compiled .exe files.\n" +
                        "For C++: Ensure folder contains public header files (include/, api/, *Interface.h).");
                    return;
                }
                
                // 2. Analyze .NET dependencies (only if .NET executables exist)
                var modules = new List<RailFactory.Core.DependencyInfo>();
                var settings = _settingsService.LoadSettings();
                var scanOptions = settings.ScanOptions ?? new RailFactory.Core.ScanOptions();
                RailFactory.Core.DependencyGraph? graph = null;
                
                if (scanResult.Executables.Count > 0)
                {
                    AppendLog("> Analyzing .NET dependencies...");
                    var analyzer = new RailFactory.Core.DependencyAnalyzer();
                    graph = analyzer.AnalyzeDependencies(
                        scanResult.Executables.Select(e => e.Path).ToList()
                    );
                    
                    AppendLog($">   Found {graph.Dependencies.Count} dependencies");
                    
                    // Debug: Show any analysis errors
                    if (analyzer.LastError != null)
                    {
                        AppendLog($">   DEBUG: {analyzer.LastError}");
                    }
                    
                    // 3. Classify assemblies
                    AppendLog("> Classifying assemblies...");
                    var classifier = new RailFactory.Core.AssemblyClassifier();
                    
                    foreach (var dep in graph.Dependencies)
                    {
                        dep.Classification = classifier.Classify(dep.Path, scanOptions);
                    }
                    
                    modules = graph.Dependencies.Where(d => d.Classification == RailFactory.Core.AssemblyClassification.Module).ToList();
                    var dependencies = graph.Dependencies.Where(d => d.Classification == RailFactory.Core.AssemblyClassification.Dependency).ToList();
                    var excluded = graph.Dependencies.Where(d => 
                        d.Classification == RailFactory.Core.AssemblyClassification.SystemFramework ||
                        d.Classification == RailFactory.Core.AssemblyClassification.ThirdParty ||
                        d.Classification == RailFactory.Core.AssemblyClassification.Excluded
                    ).ToList();
                    
                    AppendLog($">   Modules: {modules.Count + scanResult.Executables.Count}");
                    AppendLog($">   Dependencies: {dependencies.Count}");
                    AppendLog($">   Excluded: {excluded.Count}");
                }
                else
                {
                    AppendLog("> Skipping .NET analysis (no .NET executables)");
                }
                
                // 4. Show module selection dialog
                AppendLog("> Preparing module selection...");
                var solutionName = Path.GetFileName(rootPath);
                
                // Build selection items from executables and project DLLs
                var selectionItems = new List<Views.ModuleSelectionItem>();
                
                // Add executables
                foreach (var exe in scanResult.Executables)
                {
                    var managedPath = RailFactory.Core.RuntimeRegistry.GetManagedAssemblyPath(exe.Path);
                    var exeScanner = RailFactory.Core.RuntimeRegistry.GetScanner(exe.RuntimeType);
                    var methods = exeScanner.ScanBinary(managedPath, scanOptions);
                    
                    selectionItems.Add(new Views.ModuleSelectionItem
                    {
                        ModuleName = exe.Name,
                        ModulePath = exe.Path,
                        ToolCount = methods.Count,
                        IsExecutable = true,
                        IsSelected = true
                    });
                }
                
                // Add project DLLs (modules)
                foreach (var dll in modules)
                {
                    try
                    {
                        var dllScanner = RailFactory.Core.RuntimeRegistry.GetScanner(RailFactory.Core.RuntimeType.DotNetBinary);
                        var dllMethods = dllScanner.ScanBinary(dll.Path, scanOptions);
                        
                        selectionItems.Add(new Views.ModuleSelectionItem
                        {
                            ModuleName = dll.Name,
                            ModulePath = dll.Path,
                            ToolCount = dllMethods.Count,
                            IsExecutable = false,
                            IsSelected = true
                        });
                    }
                    catch { /* Skip if can't scan */ }
                }
                
                // ═══════════════════════════════════════════════════════════════
                // C++ Modules removed - Using RTTR approach in v3.0
                // ═══════════════════════════════════════════════════════════════
                
                // Show selection dialog
                var selectionDialog = new Views.ModuleSelectionDialog(selectionItems);
                selectionDialog.Owner = Application.Current.MainWindow;
                if (selectionDialog.ShowDialog() != true)
                {
                    AppendLog("> Build cancelled by user");
                    return;
                }
                
                var selectedModules = selectionDialog.SelectedModules;
                AppendLog($">   User selected: {selectedModules.Count} modules");
                
                // Show output folder dialog
                var outputSettings = _settingsService.LoadSettings();
                var folderDialog = new Views.OutputFolderDialog(outputSettings.OutputPath, $"{solutionName}_solution");
                if (folderDialog.ShowDialog() != true)
                {
                    AppendLog("> Build cancelled by user");
                    return;
                }
                
                // 5. Generate composite manifest (only for selected modules)
                AppendLog("> Generating composite manifest...");
                var manifest = GenerateCompositeManifestFromSelection(solutionName, selectedModules, graph, scanOptions, runtimeType);
                
                // 6. Save manifest
                var outputDir = Path.Combine(outputSettings.OutputPath, folderDialog.FolderName);
                Directory.CreateDirectory(outputDir);
                SaveCompositeManifest(manifest, outputDir);
                
                AppendLog($"> Composite manifest saved: {outputDir}");
                AppendLog($"> Build Solution complete! ✓");
                
                // Refresh assets tree
                LoadAssetsFromOutputFolder();
                
                _dialogService.ShowMessage("Build Complete", 
                    $"Composite manifest generated successfully!\n\n" +
                    $"Modules: {manifest.Modules.Count}\n" +
                    $"Shared dependencies: {manifest.SharedDependencies.Count}\n" +
                    $"Total tools: {manifest.Modules.Sum(m => m.Tools.Count)}");
            }
            catch (Exception ex)
            {
                AppendLog($"> ERROR: {ex.Message}");
                AppendLog($"> Stack: {ex.StackTrace}");
                _dialogService.ShowMessage("Build Failed", $"Error during solution build:\n\n{ex.Message}");
            }
        }
        
        private RailFactory.Core.CompositeManifest GenerateCompositeManifest(
            string solutionName,
            List<RailFactory.Core.ExecutableInfo> executables,
            RailFactory.Core.DependencyGraph graph,
            RailFactory.Core.ScanOptions scanOptions)
        {
            var manifest = new RailFactory.Core.CompositeManifest
            {
                SolutionName = solutionName,
                Modules = new List<RailFactory.Core.ModuleManifest>(),
                SharedDependencies = new List<RailFactory.Core.SharedDependency>(),
                Metadata = new RailFactory.Core.ManifestMetadata()
            };
            
            // Generate module for each executable
            foreach (var exe in executables)
            {
                AppendLog($">   Scanning module: {exe.Name}");
                
                // Get managed assembly path (handles native apphost wrappers)
                var managedPath = RailFactory.Core.RuntimeRegistry.GetManagedAssemblyPath(exe.Path);
                if (managedPath != exe.Path)
                {
                    AppendLog($">     Using managed DLL: {Path.GetFileName(managedPath)}");
                }
                
                var scanner = RailFactory.Core.RuntimeRegistry.GetScanner(exe.RuntimeType);
                var methods = scanner.ScanBinary(managedPath, scanOptions);
                
                AppendLog($">     Found {methods.Count} tools");
                
                // Get dependencies for this module (only non-excluded ones)
                var moduleDeps = graph.Dependencies
                    .Where(d => d.UsedBy.Contains(exe.Name) && 
                           (d.Classification == RailFactory.Core.AssemblyClassification.Dependency ||
                            d.Classification == RailFactory.Core.AssemblyClassification.Module))
                    .Select(d => Path.GetFileName(d.Path))
                    .ToList();
                
                manifest.Modules.Add(new RailFactory.Core.ModuleManifest
                {
                    ModuleId = exe.Name,
                    RuntimeType = "dotnetbinary",
                    Transport = "namedpipe", // Default for .NET binaries
                    EntryPoint = exe.Path,
                    Dependencies = moduleDeps,
                    Tools = methods.Select(m => new RailFactory.Core.ToolDefinition
                    {
                        Name = m.MethodName,
                        ClassName = m.ClassName,
                        Description = m.Description,
                        Parameters = ConvertMethodParametersToSchema(m)
                    }).ToList()
                });
            }
            
            // Scan project DLLs classified as Module (class libraries in solution)
            // These are NOT exe files but contain public tools that LLM should call
            var projectDlls = graph.Dependencies
                .Where(d => d.Classification == RailFactory.Core.AssemblyClassification.Module)
                .Where(d => !executables.Any(e => Path.GetFileNameWithoutExtension(e.Path) == Path.GetFileNameWithoutExtension(d.Path)))
                .ToList();
            
            foreach (var dll in projectDlls)
            {
                AppendLog($">   Scanning project library: {Path.GetFileName(dll.Path)}");
                
                try
                {
                    var dllScanner = RailFactory.Core.RuntimeRegistry.GetScanner(RailFactory.Core.RuntimeType.DotNetBinary);
                    var dllMethods = dllScanner.ScanBinary(dll.Path, scanOptions);
                    
                    if (dllMethods.Count > 0)
                    {
                        AppendLog($">     Found {dllMethods.Count} tools");
                        
                        manifest.Modules.Add(new RailFactory.Core.ModuleManifest
                        {
                            ModuleId = Path.GetFileNameWithoutExtension(dll.Path),
                            RuntimeType = "dotnetbinary",
                            Transport = "namedpipe", // Default for .NET binaries
                            EntryPoint = dll.Path,
                            Dependencies = new List<string>(),
                            Tools = dllMethods.Select(m => new RailFactory.Core.ToolDefinition
                            {
                                Name = m.MethodName,
                                ClassName = m.ClassName,
                                Description = m.Description,
                                Parameters = ConvertMethodParametersToSchema(m)
                            }).ToList()
                        });
                    }
                    else
                    {
                        AppendLog($">     No public tools found (skipped)");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog($">     Warning: Could not scan {Path.GetFileName(dll.Path)}: {ex.Message}");
                }
            }
            
            // Add shared dependencies
            manifest.SharedDependencies = graph.GetSharedDependencies()
                .Where(d => d.Classification == RailFactory.Core.AssemblyClassification.Dependency)
                .Select(d => new RailFactory.Core.SharedDependency
                {
                    Name = Path.GetFileName(d.Path),
                    Version = d.Version,
                    UsedBy = d.UsedBy.ToList()
                })
                .ToList();
            
            return manifest;
        }
        
        /// <summary>
        /// Generates composite manifest from user-selected modules only.
        /// Enterprise UX: User explicitly chooses what to expose to LLM.
        /// </summary>
        private RailFactory.Core.CompositeManifest GenerateCompositeManifestFromSelection(
            string solutionName,
            List<Views.ModuleSelectionItem> selectedModules,
            RailFactory.Core.DependencyGraph graph,
            RailFactory.Core.ScanOptions scanOptions, RuntimeType runtimeType)
        {
            var manifest = new RailFactory.Core.CompositeManifest
            {
                SolutionName = solutionName,
                Modules = new List<RailFactory.Core.ModuleManifest>(),
                SharedDependencies = new List<RailFactory.Core.SharedDependency>(),
                Metadata = new RailFactory.Core.ManifestMetadata()
            };
            
            foreach (var selected in selectedModules)
            {
                AppendLog($">   Scanning module: {selected.ModuleName}");
                
                try
                {
                    var modulePath = selected.ModulePath;
                    
                    // Handle executables (get managed path)
                    if (selected.IsExecutable)
                    {
                        modulePath = RailFactory.Core.RuntimeRegistry.GetManagedAssemblyPath(modulePath);
                        if (modulePath != selected.ModulePath)
                        {
                            AppendLog($">     Using managed DLL: {Path.GetFileName(modulePath)}");
                        }
                    }
                    
                    var scanner = RailFactory.Core.RuntimeRegistry.GetScanner(RailFactory.Core.RuntimeType.DotNetBinary);
                    var methods = scanner.ScanBinary(modulePath, scanOptions);
                    
                    AppendLog($">     Found {methods.Count} tools");
                    
                    manifest.Modules.Add(new RailFactory.Core.ModuleManifest
                    {
                        ModuleId = selected.ModuleName,
                        RuntimeType = ConvertToStandardRuntimeString(runtimeType),
                        Transport = "namedpipe", // Default for .NET binaries
                        EntryPoint = selected.ModulePath,
                        Dependencies = new List<string>(),
                        Tools = methods.Select(m => new RailFactory.Core.ToolDefinition
                        {
                            Name = m.MethodName,
                            ClassName = m.ClassName,
                            Description = m.Description,
                            Parameters = ConvertMethodParametersToSchema(m)
                        }).ToList()
                    });
                }
                catch (Exception ex)
                {
                    AppendLog($">     Warning: Could not scan {selected.ModuleName}: {ex.Message}");
                }
            }
            
            // Add shared dependencies (non-blacklisted only)
            manifest.SharedDependencies = graph.GetSharedDependencies()
                .Where(d => d.Classification == RailFactory.Core.AssemblyClassification.Dependency)
                .Select(d => new RailFactory.Core.SharedDependency
                {
                    Name = Path.GetFileName(d.Path),
                    Version = d.Version,
                    UsedBy = d.UsedBy.ToList()
                })
                .ToList();
            
            return manifest;
        }
        
        private Dictionary<string, object> ConvertMethodParametersToSchema(RailFactory.Core.RailMethod method)
        {
            var schema = new Dictionary<string, object>
            {
                { "type", "OBJECT" },
                { "properties", method.Parameters.ToDictionary(
                    p => p.Name,
                    p => p.ParameterSchema ?? new Dictionary<string, object>
                    {
                        { "type", ConvertTypeToGemini(p.ParameterType) },
                        { "description", p.ParameterType.Name }
                    }
                )},
                { "required", method.Parameters.Where(p => !p.IsOptional).Select(p => p.Name).ToArray() }
            };
            
            return schema;
        }
        
        private string CreateSolutionOutputFolder(string solutionName, string basePath)
        {
            var outputDir = Path.Combine(basePath, $"{solutionName}_solution");
            Directory.CreateDirectory(outputDir);
            return outputDir;
        }
        
        private void SaveCompositeManifest(RailFactory.Core.CompositeManifest manifest, string outputDir)
        {
            var manifestPath = Path.Combine(outputDir, Constants.Rail_MANIFEST_FILENAME);
            var json = System.Text.Json.JsonSerializer.Serialize(manifest, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(manifestPath, json);
            AppendLog($">   Saved: {Constants.Rail_MANIFEST_FILENAME}");
        }
        
        /// <summary>
        /// Populates Assembly, Type and Transport metadata for composite manifests.
        /// </summary>
        private void PopulateCompositeMetadata(RailFactory.Core.CompositeManifest compositeManifest)
        {
            if (compositeManifest?.Modules == null || FunctionModels.Count == 0)
                return;
                
            int functionIndex = 0;
            
            foreach (var module in compositeManifest.Modules)
            {
                if (module.Tools != null)
                {
                    foreach (var tool in module.Tools)
                    {
                        if (functionIndex < FunctionModels.Count)
                        {
                            FunctionModels[functionIndex].Assembly = module.ModuleId;
                            FunctionModels[functionIndex].Type = "Module";
                            FunctionModels[functionIndex].Transport = module.Transport ?? "namedpipe";
                            // Note: ClassName now reads directly from _tool.ClassName via delegation
                            functionIndex++;
                        }
                    }
                }
            }
        }
        
        private string ConvertToStandardRuntimeString(RailFactory.Core.RuntimeType type)
        {
            return type switch
            {
                RailFactory.Core.RuntimeType.DotNetBinary => "dotnet-ipc",
                RailFactory.Core.RuntimeType.CppBinary => "native-bridge",
                RailFactory.Core.RuntimeType.GenerativePowerShell => "generative_powershell",
                RailFactory.Core.RuntimeType.Script => "python-script",
                _ => type.ToString().ToLower()
            };
        }
    }
}




