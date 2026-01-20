using System.Windows;
using WpfRagApp.ViewModels;
using WpfRagApp.Views;

namespace WpfRagApp
{
    public partial class MainWindow : Window
    {
        private MiniPromptWindow? _miniPromptWindow;
        private MainViewModel _mainViewModel;
        
        // Window state preservation
        private WindowState _previousWindowState = WindowState.Normal;
        private double _previousWidth;
        private double _previousHeight;
        private double _previousLeft;
        private double _previousTop;
        
        public MainWindow()
        {
            InitializeComponent();
            _mainViewModel = new MainViewModel();
            DataContext = _mainViewModel;
            
            // Hook state changes
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
        }
        
        /// <summary>
        /// Handle minimize - show mini prompt instead of taskbar.
        /// </summary>
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                // Cancel minimize, show mini prompt instead
                ShowMiniPrompt();
            }
            else
            {
                // Continuously track non-minimized state so we can restore it later
                _previousWindowState = WindowState;
                
                if (WindowState == WindowState.Normal)
                {
                    // Save dimensions when Normal (Maximized fills the screen)
                    _previousWidth = Width;
                    _previousHeight = Height;
                    _previousLeft = Left;
                    _previousTop = Top;
                }
            }
        }
        
        /// <summary>
        /// Show the mini prompt window and hide main window.
        /// </summary>
        private void ShowMiniPrompt()
        {
            // Create mini window if needed
            if (_miniPromptWindow == null)
            {
                _miniPromptWindow = new MiniPromptWindow();
                
                // Share the HomeViewModel directly via the exposed property
                _miniPromptWindow.SetViewModel(_mainViewModel.HomeViewModel);
                
                _miniPromptWindow.RestoreRequested += MiniPromptWindow_RestoreRequested;
            }
            
            // Cancel minimize and hide instead
            WindowState = WindowState.Normal;
            ShowInTaskbar = false;
            Hide();
            
            // Show mini
            _miniPromptWindow.Show();
            _miniPromptWindow.Activate();
        }
        
        /// <summary>
        /// Restore main window when mini requests it.
        /// </summary>
        private void MiniPromptWindow_RestoreRequested(object? sender, EventArgs e)
        {
            RestoreFromMini();
        }
        
        /// <summary>
        /// Restore from mini prompt mode to full window.
        /// </summary>
        public void RestoreFromMini()
        {
            _miniPromptWindow?.ClosePopups();
            _miniPromptWindow?.Hide();
            
            // Restore taskbar visibility
            ShowInTaskbar = true;
            
            // Restore previous window state
            if (_previousWindowState == WindowState.Maximized)
            {
                Show();
                WindowState = WindowState.Maximized;
            }
            else
            {
                // Restore dimensions before showing
                if (_previousWidth > 0) Width = _previousWidth;
                if (_previousHeight > 0) Height = _previousHeight;
                if (_previousLeft >= 0) Left = _previousLeft;
                if (_previousTop >= 0) Top = _previousTop;
                
                Show();
                WindowState = WindowState.Normal;
            }
            
            Activate();
        }
        
        /// <summary>
        /// Clean up mini window on close.
        /// </summary>
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_miniPromptWindow != null)
            {
                _miniPromptWindow.RestoreRequested -= MiniPromptWindow_RestoreRequested;
                _miniPromptWindow.Close();
            }
        }
        
        /// <summary>
        /// Open API Configuration window.
        /// </summary>
        private void ApiConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var addWindow = new WpfRagApp.Views.ApiConfig.AddApiProviderWindow();
            addWindow.Owner = this;
            
            if (addWindow.ShowDialog() == true)
            {
                // Refresh assets after adding new provider
                _mainViewModel.HomeViewModel.RefreshAssets();
            }
        }
    }
}





