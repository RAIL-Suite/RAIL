using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfRagApp.ViewModels;

namespace WpfRagApp.Views
{
    public partial class HomeView : UserControl
    {
        public HomeView()
        {
            InitializeComponent();
        }

        public void AssetComboBox_DropDownOpened(object sender, EventArgs e)
        {
            if (DataContext is HomeViewModel viewModel)
            {
                viewModel.RefreshAssets();
            }
        }

        private void ScrollLeft_Click(object sender, RoutedEventArgs e)
        {
            if (ChipScrollViewer != null)
            {
                var offset = ChipScrollViewer.HorizontalOffset;
                ChipScrollViewer.ScrollToHorizontalOffset(offset - 100);
            }
        }

        private void ScrollRight_Click(object sender, RoutedEventArgs e)
        {
            if (ChipScrollViewer != null)
            {
                var offset = ChipScrollViewer.HorizontalOffset;
                ChipScrollViewer.ScrollToHorizontalOffset(offset + 100);
            }
        }

        private void AssetChip_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag != null && DataContext is HomeViewModel viewModel)
            {
                viewModel.SelectedAsset = border.Tag as WpfRagApp.Services.AssetInfo;
            }
        }
        
        /// <summary>
        /// Right-click on chip: Open config for API, show info for EXE
        /// </summary>
        private void AssetChip_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is WpfRagApp.Services.AssetInfo asset)
            {
                if (asset.Type == WpfRagApp.Services.AssetType.Api)
                {
                    // Open API config using provider ID (folder name)
                    var providerId = new System.IO.DirectoryInfo(asset.Path).Name;
                    OpenApiConfig(providerId);
                }
                else
                {
                    // For EXE assets, could show info or open folder
                    System.Diagnostics.Process.Start("explorer.exe", asset.Path);
                }
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// Handle mouse wheel on chip panel for horizontal scrolling.
        /// </summary>
        private void ChipScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (ChipScrollViewer != null)
            {
                // Scroll horizontally based on wheel direction
                var offset = ChipScrollViewer.HorizontalOffset;
                ChipScrollViewer.ScrollToHorizontalOffset(offset - e.Delta);
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// Handle drag over on question input for visual feedback.
        /// </summary>
        private void QuestionTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0)
                {
                    var ext = System.IO.Path.GetExtension(files[0]).ToLowerInvariant();
                    // Accept Excel, CSV, JSON files
                    if (ext == ".xlsx" || ext == ".xls" || ext == ".csv" || ext == ".json")
                    {
                        e.Effects = DragDropEffects.Copy;
                        e.Handled = true;
                        return;
                    }
                }
            }
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }
        
        /// <summary>
        /// Handle file drop on question input - triggers data ingestion pipeline.
        /// </summary>
        private async void QuestionTextBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files?.Length > 0 && DataContext is HomeViewModel viewModel)
                {
                    var filePath = files[0];
                    try
                    {
                        await viewModel.HandleFileDropAsync(filePath);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to process file: {ex.Message}", "Import Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        
        #region Voice Input
        
        private WpfRagApp.Services.AudioRecorderService? _recorder;
        private WpfRagApp.Services.TextToSpeechService? _tts;
        private bool _isRecording;
        private string? _lastLlmAnswer;
        
        /// <summary>
        /// Enter key sends the text prompt.
        /// </summary>
        private async void QuestionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                if (DataContext is HomeViewModel viewModel && viewModel.AskCommand.CanExecute(null))
                {
                    await viewModel.AskAsync();
                    _lastLlmAnswer = ExtractLlmAnswer(viewModel.Answer);
                    // Don't auto-speak on Enter - user can click speaker button to hear
                }
            }
        }
        
        /// <summary>
        /// Mic button toggles recording with visual feedback.
        /// </summary>
        private async void MicButton_Click(object sender, RoutedEventArgs e)
        {
            _recorder ??= new WpfRagApp.Services.AudioRecorderService();
            
            if (!_isRecording)
            {
                // Start recording
                _isRecording = true;
                StartRecordingVisuals();
                _recorder.StartRecording();
            }
            else
            {
                // Stop recording and send to LLM
                _isRecording = false;
                StopRecordingVisuals();
                
                var audioBytes = _recorder.StopRecording();
                
                if (audioBytes.Length > 0 && DataContext is HomeViewModel viewModel)
                {
                    await viewModel.AskWithAudioAsync(audioBytes);
                    _lastLlmAnswer = ExtractLlmAnswer(viewModel.Answer);
                    AutoSpeak(_lastLlmAnswer);
                }
            }
        }
        
        private System.Windows.Media.Animation.Storyboard? _blinkStoryboard;
        
        private void StartRecordingVisuals()
        {
            MicButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 53, 69)); // Red
            RecordingDot.Visibility = Visibility.Visible;
            
            // Start blinking animation
            _blinkStoryboard = (System.Windows.Media.Animation.Storyboard)MicButton.Resources["BlinkAnimation"];
            _blinkStoryboard?.Begin(MicButton, true);
        }
        
        private void StopRecordingVisuals()
        {
            // Stop blinking
            _blinkStoryboard?.Stop(MicButton);
            
            MicButton.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204)); // Blue
            MicButton.Opacity = 1.0;
            RecordingDot.Visibility = Visibility.Collapsed;
        }
        
        // TTS enabled by default
        private bool _ttsEnabled = true;
        
        /// <summary>
        /// Auto-speak the LLM answer if TTS is enabled.
        /// </summary>
        private void AutoSpeak(string? text)
        {
            if (!_ttsEnabled || string.IsNullOrWhiteSpace(text)) return;
            
            _tts ??= new WpfRagApp.Services.TextToSpeechService();
            _tts.Speak(text);
        }
        
        /// <summary>
        /// Left click: Play/Pause or Replay last answer.
        /// </summary>
        private void SpeakerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_ttsEnabled) return; // Muted, do nothing
            
            _tts ??= new WpfRagApp.Services.TextToSpeechService();
            
            if (_tts.IsSpeaking)
            {
                _tts.Stop();
            }
            else if (!string.IsNullOrWhiteSpace(_lastLlmAnswer))
            {
                _tts.Speak(_lastLlmAnswer);
            }
        }
        
        /// <summary>
        /// Right click: Toggle mute on/off.
        /// When unmuted, doesn't replay - just waits for next response.
        /// </summary>
        private void SpeakerButton_RightClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _tts ??= new WpfRagApp.Services.TextToSpeechService();
            
            _ttsEnabled = !_ttsEnabled;
            
            if (!_ttsEnabled)
            {
                _tts.Stop();
                SpeakerIcon.Text = "🔇"; // Muted icon
                SpeakerButton.Opacity = 0.6;
            }
            else
            {
                SpeakerIcon.Text = "🔊"; // Unmuted icon
                SpeakerButton.Opacity = 1.0;
                // Don't replay - just wait for next response
            }
            
            e.Handled = true;
        }
        
        /// <summary>
        /// Extract only the LLM answer from the response (removes our formatting).
        /// </summary>
        private string ExtractLlmAnswer(string fullResponse)
        {
            if (string.IsNullOrWhiteSpace(fullResponse)) return "";
            
            // Look for our "Answer:" marker
            var answerMarker = "**Answer:**";
            var idx = fullResponse.LastIndexOf(answerMarker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return fullResponse.Substring(idx + answerMarker.Length).Trim();
            }
            
            // If no marker, try to extract text after last "---"
            var lastSeparator = fullResponse.LastIndexOf("---");
            if (lastSeparator >= 0)
            {
                var afterSep = fullResponse.Substring(lastSeparator + 3).Trim();
                // Remove emoji markers
                if (afterSep.StartsWith("✅"))
                    afterSep = afterSep.Substring(1).Trim();
                return afterSep;
            }
            
            // Fallback: return whole response
            return fullResponse;
        }
        
        #endregion
        
        #region API Chip Handlers
        
        /// <summary>
        /// API Chip left-click: Show config or status
        /// </summary>
        private void ApiChip_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border && border.Tag is string providerId)
            {
                // For now, just open config - later can show status popup
                OpenApiConfig(providerId);
            }
        }
        
        /// <summary>
        /// API Chip right-click: Open configuration window
        /// </summary>
        private void ApiChip_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.Border border && border.Tag is string providerId)
            {
                OpenApiConfig(providerId);
                e.Handled = true;
            }
        }
        
        private void OpenApiConfig(string providerId)
        {
            try
            {
                var configWindow = new WpfRagApp.Views.ApiConfig.ApiConfigWindow(providerId);
                configWindow.Owner = Window.GetWindow(this);
                configWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening config: {ex.Message}", "Error");
            }
        }
        
        /// <summary>
        /// Add API button click: Open import wizard
        /// </summary>
        private void AddApiChip_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var importWindow = new WpfRagApp.Views.ApiConfig.ApiImportWindow();
                importWindow.Owner = Window.GetWindow(this);
                
                if (importWindow.ShowDialog() == true && importWindow.ImportSuccessful)
                {
                    // Could refresh the chip list here
                    System.Windows.MessageBox.Show(
                        $"Successfully imported {importWindow.ImportedSkillCount} skills from '{importWindow.ImportedProviderId}'!",
                        "Import Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening import: {ex.Message}", "Error");
            }
        }
        
        #endregion
    }
}






