using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using WpfRagApp.Services;

namespace WpfRagApp.ViewModels
{
    public class ModelInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class HomeViewModel : ViewModelBase
    {
        private readonly LLMService _llmService;
        private readonly SettingsService _settingsService;
        private readonly AssetService _assetService;
        private string _question = string.Empty;
        private string _answer = "Select a model and ask a question.";
        private ModelInfo _selectedModel;
        private AssetInfo _selectedAsset;
        private bool _isBusy;
        private double _temperature;
        private bool _reActEnabled;
        private bool _chatOnlyMode;
        private bool _reActStateBeforeChatOnly;

        public HomeViewModel(LLMService llmService, SettingsService settingsService)
        {
            _llmService = llmService;
            _settingsService = settingsService;
            _assetService = new AssetService(settingsService); // Pass settings for configurable path

            AvailableModels = new ObservableCollection<ModelInfo>
            {
                new ModelInfo { Id = "gemini-2.5-flash-lite", Name = "Gemini 2.5 Flash Lite" },
                new ModelInfo { Id = "gemini-2.5-flash", Name = "Gemini 2.5 Flash" },
                new ModelInfo { Id = "gemini-2.5-pro", Name = "Gemini 2.5 Pro" },
                new ModelInfo { Id = "gpt-4o", Name = "GPT-4o" },
                new ModelInfo { Id = "gpt-5-mini", Name = "GPT-5 Mini" },
                new ModelInfo { Id = "claude-sonnet-4-5-20250929", Name = "Claude Sonnet 4.5" }
            };

            // Restore selected model from settings
            var savedModelId = _settingsService.SelectedModelId;
            _selectedModel = AvailableModels.FirstOrDefault(m => m.Id == savedModelId) ?? AvailableModels.First();

            // Restore temperature from settings
            _temperature = _settingsService.Temperature;

            // Restore ReAct mode from settings
            _reActEnabled = _settingsService.ReActEnabled;

            // Load Assets
            var assets = _assetService.GetAssets();
            AvailableAssets = new ObservableCollection<AssetInfo>(assets);
            
            // Default to first asset (Chat Only is now via toggle button)
            _selectedAsset = AvailableAssets.FirstOrDefault();
            
            if (_selectedAsset != null)
            {
                _llmService.SetAsset(_selectedAsset.Path);
            }

            // Subscribe to settings changes for reactive UI updates
            _settingsService.SettingChanged += OnSettingChanged;

            AskCommand = new RelayCommand(async _ => await AskAsync(), _ => !IsBusy && !string.IsNullOrWhiteSpace(Question));
        }

        /// <summary>
        /// Handle settings changes - refresh UI when relevant settings change.
        /// </summary>
        private void OnSettingChanged(object? sender, string propertyName)
        {
            if (propertyName == nameof(SettingsService.AssetsRootPath))
            {
                // Refresh assets on UI thread
                System.Windows.Application.Current?.Dispatcher.Invoke(RefreshAssets);
            }
        }

        public ObservableCollection<ModelInfo> AvailableModels { get; }
        public ObservableCollection<AssetInfo> AvailableAssets { get; }

        public ModelInfo SelectedModel
        {
            get => _selectedModel;
            set
            {
                if (SetProperty(ref _selectedModel, value))
                {
                    _settingsService.SelectedModelId = value.Id;
                }
            }
        }

        public AssetInfo SelectedAsset
        {
            get => _selectedAsset;
            set
            {
                // Clear previous selection
                if (_selectedAsset != null)
                {
                    _selectedAsset.IsSelected = false;
                }
                
                if (SetProperty(ref _selectedAsset, value))
                {
                    if (value != null)
                    {
                        value.IsSelected = true;
                        _llmService.SetAsset(value.Path);
                    }
                }
            }
        }

        public string Question
        {
            get => _question;
            set => SetProperty(ref _question, value);
        }

        public string Answer
        {
            get => _answer;
            set => SetProperty(ref _answer, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public double Temperature
        {
            get => _temperature;
            set
            {
                if (SetProperty(ref _temperature, value))
                {
                    _settingsService.Temperature = value;
                }
            }
        }

        public bool ReActEnabled
        {
            get => _reActEnabled;
            set
            {
                if (SetProperty(ref _reActEnabled, value))
                {
                    _settingsService.ReActEnabled = value;
                }
            }
        }

        public bool ChatOnlyMode
        {
            get => _chatOnlyMode;
            set
            {
                if (SetProperty(ref _chatOnlyMode, value))
                {
                    if (value)
                    {
                        // Entering Chat Only mode - save ReAct state and disable tools
                        _reActStateBeforeChatOnly = ReActEnabled;
                        _llmService.SetAsset(string.Empty);
                        ReActEnabled = false;
                    }
                    else
                    {
                        // Exiting Chat Only mode - restore asset and ReAct state
                        if (_selectedAsset != null)
                        {
                            _llmService.SetAsset(_selectedAsset.Path);
                        }
                        ReActEnabled = _reActStateBeforeChatOnly;
                    }
                    
                    // Notify computed properties
                    OnPropertyChanged(nameof(IsReActAvailable));
                    OnPropertyChanged(nameof(IsAssetPanelEnabled));
                    OnPropertyChanged(nameof(AssetPanelOpacity));
                }
            }
        }

        /// <summary>
        /// ReAct is only available when NOT in Chat Only mode.
        /// </summary>
        public bool IsReActAvailable => !ChatOnlyMode;

        /// <summary>
        /// Asset panel is enabled when NOT in Chat Only mode.
        /// </summary>
        public bool IsAssetPanelEnabled => !ChatOnlyMode;

        /// <summary>
        /// Asset panel opacity: 1.0 when enabled, 0.4 when ghosted.
        /// </summary>
        public double AssetPanelOpacity => ChatOnlyMode ? 0.4 : 1.0;

        public ICommand AskCommand { get; }

        public async Task AskAsync()
        {
            if (string.IsNullOrWhiteSpace(Question)) return;

            IsBusy = true;
            Answer = "Thinking...";

            try
            {
                // Dual-mode routing
                bool isBulkMode = HasAttachedFile && AttachedFile!.TotalRowCount > 1;
                
                if (isBulkMode)
                {
                    await ExecuteBulkModeAsync();
                }
                else
                {
                    await ExecuteNormalModeAsync();
                }
            }
            finally
            {
                IsBusy = false;
                ClearAttachedFile();
            }
        }
        
        /// <summary>
        /// Ask with voice input (audio bytes from microphone).
        /// </summary>
        public async Task AskWithAudioAsync(byte[] audioBytes)
        {
            if (audioBytes.Length == 0) return;

            IsBusy = true;
            Answer = "🎤 Processing voice...";

            try
            {
                var response = await _llmService.ChatWithAudioAsync(audioBytes, SelectedModel.Id, Temperature);
                Answer = response;
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        /// <summary>
        /// Normal mode: ReAct as usual.
        /// </summary>
        private async Task ExecuteNormalModeAsync()
        {
            var enrichedPrompt = Question;
            if (HasAttachedFile)
            {
                enrichedPrompt += GetFileContextForPrompt();
            }
            
            var response = await _llmService.ChatAsync(enrichedPrompt, SelectedModel.Id, Temperature);
            Answer = response;
        }
        
        /// <summary>
        /// Bulk mode: LLM plans, code executes.
        /// </summary>
        private async Task ExecuteBulkModeAsync()
        {
            if (AttachedFile == null) return;
            
            Answer = "📊 Analyzing file and creating execution plan...";
            
            // Step 1: Get file content
            var fileContent = GetFileContextForPrompt();
            
            // Step 2: Ask LLM for execution plan (NOT execute)
            var planJson = await _llmService.GetBulkExecutionPlanAsync(
                Question, 
                fileContent, 
                SelectedModel.Id, 
                0.2);
            
            // Step 3: Parse plan
            var plan = WpfRagApp.Services.BulkExecution.ExecutionPlan.TryParse(planJson);
            
            if (plan == null)
            {
                Answer = $"❌ Failed to parse execution plan.\n\nLLM Response:\n{planJson}";
                return;
            }
            
            // Step 4: Show plan summary
            var ops = plan.Operations.Select(o => $"• {o.Function}: {o.Calls.Count} calls");
            Answer = $"📋 Execution Plan:\n{string.Join("\n", ops)}\n\n⏳ Executing...";
            
            // Step 5: Execute with BulkExecutionService
            var engine = _llmService.GetEngine();
            if (engine == null)
            {
                Answer = "❌ No RailEngine available. Please select an asset.";
                return;
            }
            
            var executor = new WpfRagApp.Services.BulkExecution.BulkExecutionService(engine);
            var progress = new Progress<WpfRagApp.Services.BulkExecution.BulkProgress>(p =>
            {
                Answer = $"⏳ Executing {p.CurrentFunction}... ({p.Current}/{p.Total})";
            });
            
            var report = await executor.ExecuteAsync(plan, progress);
            
            // Step 6: Show report
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"✅ Operation complete!");
            sb.AppendLine($"   Success: {report.SuccessCount}/{report.TotalCount}");
            
            if (report.HasErrors)
            {
                sb.AppendLine($"\n❌ Errors ({report.Errors.Count}):");
                foreach (var err in report.Errors.Take(5))
                {
                    sb.AppendLine($"   Row {err.RowNumber}: {err.Message}");
                }
                if (report.Errors.Count > 5)
                    sb.AppendLine($"   ...and {report.Errors.Count - 5} more");
            }
            
            Answer = sb.ToString();
        }

        public void RefreshAssets()
        {
            var currentPath = SelectedAsset?.Path;
            var assets = _assetService.GetAssets();
            
            AvailableAssets.Clear();
            foreach (var asset in assets)
            {
                AvailableAssets.Add(asset);
            }

            // Restore selection if possible
            if (!string.IsNullOrEmpty(currentPath))
            {
                var match = AvailableAssets.FirstOrDefault(a => a.Path == currentPath);
                if (match != null)
                {
                    SelectedAsset = match;
                }
                else
                {
                    // If current asset is gone, select first or null
                    SelectedAsset = AvailableAssets.FirstOrDefault();
                }
            }
            else
            {
                SelectedAsset = AvailableAssets.FirstOrDefault();
            }
        }
        
        #region File Attachment
        
        private WpfRagApp.Services.DataIngestion.Models.ParsedData? _attachedFile;
        
        /// <summary>
        /// Currently attached file data (headers + sample rows).
        /// Cleared after prompt is sent.
        /// </summary>
        public WpfRagApp.Services.DataIngestion.Models.ParsedData? AttachedFile
        {
            get => _attachedFile;
            private set
            {
                if (SetProperty(ref _attachedFile, value))
                {
                    OnPropertyChanged(nameof(HasAttachedFile));
                    OnPropertyChanged(nameof(AttachedFileName));
                }
            }
        }
        
        /// <summary>
        /// True if a file is attached.
        /// </summary>
        public bool HasAttachedFile => _attachedFile != null;
        
        /// <summary>
        /// Display name of attached file.
        /// </summary>
        public string AttachedFileName => _attachedFile != null 
            ? System.IO.Path.GetFileName(_attachedFile.SourceFile) 
            : string.Empty;
        
        /// <summary>
        /// Handle file drop - parses file and attaches to next prompt.
        /// </summary>
        public async Task HandleFileDropAsync(string filePath)
        {
            if (IsBusy)
                return;
            
            try
            {
                IsBusy = true;
                var fileName = System.IO.Path.GetFileName(filePath);
                Answer = $"📎 Parsing: {fileName}...";
                
                // Parse file using DataIngestion router
                var router = new WpfRagApp.Services.DataIngestion.Routing.FileTypeDetector();
                var fileType = router.DetectType(filePath);
                
                if (fileType == WpfRagApp.Services.DataIngestion.Interfaces.FileType.Unknown)
                {
                    Answer = $"❌ Unsupported file type: {System.IO.Path.GetExtension(filePath)}";
                    return;
                }
                
                if (router.RequiresAIExtraction(fileType))
                {
                    Answer = $"❌ {fileType} files require AI extraction (not yet implemented)";
                    return;
                }
                
                var parser = router.GetParser(fileType);
                AttachedFile = parser.ParseSample(filePath);
                
                // Show confirmation
                var headers = string.Join(", ", AttachedFile.Headers.Take(5));
                if (AttachedFile.Headers.Length > 5) headers += "...";
                
                Answer = $"📎 File attached: {fileName}\n" +
                         $"   Rows: {AttachedFile.TotalRowCount:N0}\n" +
                         $"   Columns: {headers}\n\n" +
                         "💡 Now write your prompt (e.g., 'Add these users to CRM')";
            }
            catch (System.Exception ex)
            {
                Answer = $"❌ Failed to parse file: {ex.Message}";
                AttachedFile = null;
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        /// <summary>
        /// Clear attached file.
        /// </summary>
        public void ClearAttachedFile()
        {
            AttachedFile = null;
        }
        
        /// <summary>
        /// Get file context to append to user prompt for AI.
        /// Sends ALL rows for complete processing.
        /// </summary>
        public string GetFileContextForPrompt()
        {
            if (AttachedFile == null)
                return string.Empty;
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"📎 File: {System.IO.Path.GetFileName(AttachedFile.SourceFile)}");
            sb.AppendLine($"   Columns: {string.Join(", ", AttachedFile.Headers)}");
            sb.AppendLine($"   Data ({AttachedFile.TotalRowCount} rows):");
            
            // Read ALL rows from file
            var router = new WpfRagApp.Services.DataIngestion.Routing.FileTypeDetector();
            var parser = router.GetParser(AttachedFile.FileType);
            
            int rowNum = 0;
            foreach (var row in parser.StreamRows(AttachedFile.SourceFile))
            {
                rowNum++;
                var values = AttachedFile.Headers
                    .Select(h => row.TryGetValue(h, out var v) ? v?.ToString() ?? "" : "")
                    .ToArray();
                sb.AppendLine($"   Row {rowNum}: {string.Join(" | ", values)}");
            }
            
            sb.AppendLine();
            sb.AppendLine($"Execute the requested action for ALL {rowNum} rows above.");
            
            return sb.ToString();
        }
        
        #endregion
    }
}





