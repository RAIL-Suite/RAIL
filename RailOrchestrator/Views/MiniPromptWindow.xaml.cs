using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using WpfRagApp.ViewModels;

namespace WpfRagApp.Views
{
    /// <summary>
    /// Mini Prompt Window - floating compact prompt bar.
    /// Appears when main window is minimized, allows quick command input.
    /// </summary>
    public partial class MiniPromptWindow : Window
    {
        /// <summary>
        /// Event raised when user wants to restore the full main window.
        /// </summary>
        public event EventHandler? RestoreRequested;
        
        public MiniPromptWindow()
        {
            InitializeComponent();
            PositionBottomRight();
        }
        
        /// <summary>
        /// Sets the shared ViewModel from MainWindow.
        /// </summary>
        public void SetViewModel(HomeViewModel viewModel)
        {
            DataContext = viewModel;
        }
        
        /// <summary>
        /// Close all popups (call before hiding).
        /// </summary>
        public void ClosePopups()
        {
            ResponsePopupToggle.IsChecked = false;
            AssetPopupToggle.IsChecked = false;
        }
        
        /// <summary>
        /// Position window in bottom-right corner of screen.
        /// </summary>
        private void PositionBottomRight()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 20;
            Top = workArea.Bottom - Height - 20;
        }
        
        /// <summary>
        /// Handle drag movement when clicking on the border.
        /// </summary>
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // Double-click to restore
                if (e.ClickCount == 2)
                {
                    RestoreRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }
                
                // Single click - start drag
                DragMove();
            }
        }
        
        /// <summary>
        /// Expand button clicked - restore main window.
        /// </summary>
        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            RestoreRequested?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Handle Enter key to send prompt.
        /// </summary>
        private async void PromptInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                var vm = DataContext as HomeViewModel;
                if (vm?.AskCommand.CanExecute(null) == true)
                {
                    await vm.AskAsync();
                    _lastLlmAnswer = ExtractLlmAnswer(vm.Answer);
                    // Don't auto-speak on Enter - user can click speaker button to hear
                }
            }
            else if (e.Key == Key.Escape)
            {
                // Escape to restore main window
                RestoreRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        
        /// <summary>
        /// Focus the input when window is shown.
        /// </summary>
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            PromptInput.Focus();
        }
        
        /// <summary>
        /// Top-Left corner resize.
        /// </summary>
        private void ResizeGripTL_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // Top-Left: Move window and resize from top-left
            double newWidth = Width - e.HorizontalChange;
            double newHeight = Height - e.VerticalChange;
            
            if (newWidth >= MinWidth)
            {
                Width = newWidth;
                Left += e.HorizontalChange;
            }
            
            if (newHeight >= MinHeight)
            {
                Height = newHeight;
                Top += e.VerticalChange;
            }
        }
        
        /// <summary>
        /// Top-Right corner resize.
        /// </summary>
        private void ResizeGripTR_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // Top-Right: Width grows right, height from top
            double newWidth = Width + e.HorizontalChange;
            double newHeight = Height - e.VerticalChange;
            
            if (newWidth >= MinWidth)
            {
                Width = newWidth;
            }
            
            if (newHeight >= MinHeight)
            {
                Height = newHeight;
                Top += e.VerticalChange;
            }
        }
        
        /// <summary>
        /// Bottom-Left corner resize.
        /// </summary>
        private void ResizeGripBL_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // Bottom-Left: Width from left, height grows down
            double newWidth = Width - e.HorizontalChange;
            double newHeight = Height + e.VerticalChange;
            
            if (newWidth >= MinWidth)
            {
                Width = newWidth;
                Left += e.HorizontalChange;
            }
            
            if (newHeight >= MinHeight)
            {
                Height = newHeight;
            }
        }
        
        /// <summary>
        /// Bottom-Right corner resize.
        /// </summary>
        private void ResizeGripBR_DragDelta(object sender, DragDeltaEventArgs e)
        {
            // Bottom-Right: Both grow outward
            double newWidth = Width + e.HorizontalChange;
            double newHeight = Height + e.VerticalChange;
            
            if (newWidth >= MinWidth)
            {
                Width = newWidth;
            }
            
            if (newHeight >= MinHeight)
            {
                Height = newHeight;
            }
        }
        
        /// <summary>
        /// Handle mouse wheel scroll in asset popup.
        /// </summary>
        private void PopupScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer sv)
            {
                sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta);
                e.Handled = true;
            }
        }
        
        /// <summary>
        /// Handle chip click in popup - select asset.
        /// </summary>
        private void PopupChip_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && 
                border.Tag is WpfRagApp.Services.AssetInfo asset &&
                DataContext is HomeViewModel vm)
            {
                vm.SelectedAsset = asset;
                AssetPopupToggle.IsChecked = false; // Close popup
            }
        }
        
        /// <summary>
        /// Reset scroll position when popup opens.
        /// </summary>
        private void AssetPopup_Opened(object sender, EventArgs e)
        {
            // Scroll to beginning to show first assets
            PopupChipScrollViewer?.ScrollToHorizontalOffset(0);
        }
        
        /// <summary>
        /// Handle response popup opacity slider change.
        /// </summary>
        private void ResponseOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Apply opacity to response popup border
            if (ResponseBorder != null)
            {
                ResponseBorder.Opacity = e.NewValue;
            }
        }
        
        // Popup drag tracking
        private Point _popupDragStart;
        private bool _isDraggingPopup;
        private bool _isResizing;
        
        /// <summary>
        /// Handle drag on response popup.
        /// </summary>
        private void ResponseHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Don't start drag if resizing
            if (_isResizing) return;
            
            if (e.ChangedButton == MouseButton.Left)
            {
                // Check if we're in a corner zone (20px from edges)
                var pos = e.GetPosition(ResponseBorder);
                double cornerSize = 20;
                bool inCorner = (pos.X < cornerSize || pos.X > ResponseBorder.ActualWidth - cornerSize) &&
                                (pos.Y < cornerSize || pos.Y > ResponseBorder.ActualHeight - cornerSize);
                
                if (inCorner) return; // Let resize handle it
                
                _isDraggingPopup = true;
                _popupDragStart = e.GetPosition(this);
                ResponseBorder.CaptureMouse();
            }
        }
        
        private void ResponseHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingPopup && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPos = e.GetPosition(this);
                var delta = currentPos - _popupDragStart;
                
                ResponsePopup.HorizontalOffset += delta.X;
                ResponsePopup.VerticalOffset += delta.Y;
                
                _popupDragStart = currentPos;
            }
            else if (_isDraggingPopup)
            {
                // Mouse button released without MouseUp event
                _isDraggingPopup = false;
                ResponseBorder.ReleaseMouseCapture();
            }
        }
        
        private void ResponseHeader_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingPopup = false;
            ResponseBorder.ReleaseMouseCapture();
        }
        
        /// <summary>
        /// When resize starts, block drag.
        /// </summary>
        private void ResponseResize_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isResizing = true;
            _isDraggingPopup = false;
        }
        
        /// <summary>
        /// When resize ends, re-enable drag.
        /// </summary>
        private void ResponseResize_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isResizing = false;
        }
        
        /// <summary>
        /// Resize from Top-Left corner.
        /// </summary>
        private void ResponseResizeTL_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = ResponseBorder.ActualWidth - e.HorizontalChange;
            double newHeight = ResponseBorder.ActualHeight - e.VerticalChange;
            
            if (newWidth > 100) ResponseBorder.Width = newWidth;
            if (newHeight > 50) ResponseBorder.Height = newHeight;
        }
        
        /// <summary>
        /// Resize from Top-Right corner.
        /// </summary>
        private void ResponseResizeTR_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = ResponseBorder.ActualWidth + e.HorizontalChange;
            double newHeight = ResponseBorder.ActualHeight - e.VerticalChange;
            
            if (newWidth > 100) ResponseBorder.Width = newWidth;
            if (newHeight > 50) ResponseBorder.Height = newHeight;
        }
        
        /// <summary>
        /// Resize from Bottom-Left corner.
        /// </summary>
        private void ResponseResizeBL_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = ResponseBorder.ActualWidth - e.HorizontalChange;
            double newHeight = ResponseBorder.ActualHeight + e.VerticalChange;
            
            if (newWidth > 100) ResponseBorder.Width = newWidth;
            if (newHeight > 50) ResponseBorder.Height = newHeight;
        }
        
        /// <summary>
        /// Resize from Bottom-Right corner.
        /// </summary>
        private void ResponseResizeBR_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double newWidth = ResponseBorder.ActualWidth + e.HorizontalChange;
            double newHeight = ResponseBorder.ActualHeight + e.VerticalChange;
            
            if (newWidth > 100) ResponseBorder.Width = newWidth;
            if (newHeight > 50) ResponseBorder.Height = newHeight;
        }
        
        #region Asset Popup Drag & Resize
        
        // Asset popup drag tracking
        private Point _assetDragStart;
        private bool _isDraggingAsset;
        
        /// <summary>
        /// Handle drag on asset popup header.
        /// </summary>
        private void AssetHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var header = sender as Border;
                if (header != null)
                {
                    _isDraggingAsset = true;
                    _assetDragStart = e.GetPosition(this);
                    header.CaptureMouse();
                    header.MouseMove += AssetHeader_MouseMove;
                    header.MouseUp += AssetHeader_MouseUp;
                }
            }
        }
        
        private void AssetHeader_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingAsset)
            {
                var currentPos = e.GetPosition(this);
                var delta = currentPos - _assetDragStart;
                
                AssetPopup.HorizontalOffset += delta.X;
                AssetPopup.VerticalOffset += delta.Y;
                
                _assetDragStart = currentPos;
            }
        }
        
        private void AssetHeader_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingAsset = false;
            var header = sender as Border;
            if (header != null)
            {
                header.ReleaseMouseCapture();
                header.MouseMove -= AssetHeader_MouseMove;
                header.MouseUp -= AssetHeader_MouseUp;
            }
        }
        
        // Asset popup resize handlers (proportional)
        private void AssetResizeTL_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double aspectRatio = AssetBorder.ActualWidth / AssetBorder.ActualHeight;
            double newWidth = AssetBorder.ActualWidth - e.HorizontalChange;
            double newHeight = newWidth / aspectRatio;
            
            AssetBorder.Width = newWidth;
            AssetBorder.Height = newHeight;
            AssetPopup.HorizontalOffset += e.HorizontalChange;
            AssetPopup.VerticalOffset += (AssetBorder.ActualHeight - newHeight);
        }
        
        private void AssetResizeTR_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double aspectRatio = AssetBorder.ActualWidth / AssetBorder.ActualHeight;
            double newWidth = AssetBorder.ActualWidth + e.HorizontalChange;
            double newHeight = newWidth / aspectRatio;
            
            double heightDelta = newHeight - AssetBorder.ActualHeight;
            AssetBorder.Width = newWidth;
            AssetBorder.Height = newHeight;
            AssetPopup.VerticalOffset -= heightDelta;
        }
        
        private void AssetResizeBL_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double aspectRatio = AssetBorder.ActualWidth / AssetBorder.ActualHeight;
            double newWidth = AssetBorder.ActualWidth - e.HorizontalChange;
            double newHeight = newWidth / aspectRatio;
            
            AssetBorder.Width = newWidth;
            AssetBorder.Height = newHeight;
            AssetPopup.HorizontalOffset += e.HorizontalChange;
        }
        
        private void AssetResizeBR_DragDelta(object sender, DragDeltaEventArgs e)
        {
            double aspectRatio = AssetBorder.ActualWidth / AssetBorder.ActualHeight;
            double newWidth = AssetBorder.ActualWidth + e.HorizontalChange;
            double newHeight = newWidth / aspectRatio;
            
            AssetBorder.Width = newWidth;
            AssetBorder.Height = newHeight;
        }
        
        #endregion
        
        #region File Drop
        
        /// <summary>
        /// Handle drag over on main pill for visual feedback.
        /// </summary>
        private void MainBorder_PreviewDragOver(object sender, DragEventArgs e)
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
        /// Handle file drop on main pill - triggers data ingestion pipeline.
        /// </summary>
        private async void MainBorder_Drop(object sender, DragEventArgs e)
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
                    catch (System.Exception ex)
                    {
                        MessageBox.Show($"Failed to process file: {ex.Message}", "Import Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        
        #endregion
        
        #region Voice Input
        
        private WpfRagApp.Services.AudioRecorderService? _recorder;
        private WpfRagApp.Services.TextToSpeechService? _tts;
        private bool _isRecording;
        private string? _lastLlmAnswer;
        private bool _ttsEnabled = true; // Auto-speak enabled by default
        
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
        }
        
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
                SpeakerButton.Opacity = 0.6;
            }
            else
            {
                SpeakerButton.Opacity = 1.0;
                // Don't replay - just wait for next response
            }
            
            e.Handled = true;
        }
        
        /// <summary>
        /// Extract only the LLM answer from the response.
        /// </summary>
        private string ExtractLlmAnswer(string fullResponse)
        {
            if (string.IsNullOrWhiteSpace(fullResponse)) return "";
            
            var answerMarker = "**Answer:**";
            var idx = fullResponse.LastIndexOf(answerMarker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return fullResponse.Substring(idx + answerMarker.Length).Trim();
            }
            
            var lastSeparator = fullResponse.LastIndexOf("---");
            if (lastSeparator >= 0)
            {
                var afterSep = fullResponse.Substring(lastSeparator + 3).Trim();
                if (afterSep.StartsWith("✅"))
                    afterSep = afterSep.Substring(1).Trim();
                return afterSep;
            }
            
            return fullResponse;
        }
        
        #endregion
    }
}





