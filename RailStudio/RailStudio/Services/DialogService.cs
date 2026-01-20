using Microsoft.Win32;
using System.Windows;

namespace RailStudio.Services
{
    public interface IDialogService
    {
        string OpenFile(string filter);
        string OpenFolder();
        void ShowMessage(string title, string message);
    }

    public class DialogService : IDialogService
    {
        public string OpenFile(string filter)
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FileName;
            }

            return string.Empty;
        }

        public string OpenFolder()
        {
            var dialog = new OpenFolderDialog();

            if (dialog.ShowDialog() == true)
            {
                return dialog.FolderName;
            }

            return string.Empty;
        }

        public void ShowMessage(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}




