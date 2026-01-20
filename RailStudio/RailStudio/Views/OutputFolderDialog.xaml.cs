using System.IO;
using System.Windows;
using System.Windows.Input;

namespace RailStudio.Views
{
    public partial class OutputFolderDialog : Window
    {
        public string FolderName { get; private set; } = string.Empty;
        public string PreviewPath { get; private set; } = string.Empty;
        
        private readonly string _basePath;
        
        public OutputFolderDialog(string basePath, string suggestedName)
        {
            InitializeComponent();
            
            _basePath = basePath;
            FolderNameTextBox.Text = suggestedName;
            UpdatePreviewPath();
            
            FolderNameTextBox.TextChanged += (s, e) => UpdatePreviewPath();
            FolderNameTextBox.Focus();
            FolderNameTextBox.SelectAll();
            
            DataContext = this;
        }
        
        private void UpdatePreviewPath()
        {
            var folderName = FolderNameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(folderName))
            {
                PreviewPath = "<enter folder name>";
            }
            else
            {
                PreviewPath = Path.Combine(_basePath, folderName);
            }
            PreviewPathText.Text = PreviewPath;
        }
        
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            var folderName = FolderNameTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(folderName))
            {
                MessageBox.Show("Please enter a folder name.", "Invalid Name", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Validate folder name (no invalid chars)
            var invalidChars = Path.GetInvalidFileNameChars();
            if (folderName.IndexOfAny(invalidChars) >= 0)
            {
                MessageBox.Show("Folder name contains invalid characters.", "Invalid Name", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            FolderName = folderName;
            DialogResult = true;
            Close();
        }
        
        private void FolderNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OkButton_Click(sender, e);
            }
        }
    }
}




