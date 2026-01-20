using System.Windows;
using WpfRagApp.Services.ApiOrchestration;
using WpfRagApp.Services.ApiOrchestration.Ingestion;

namespace WpfRagApp.Views.ApiConfig;

/// <summary>
/// API Import Window - import OpenAPI/Swagger specifications.
/// </summary>
public partial class ApiImportWindow : Window
{
    public bool ImportSuccessful { get; private set; }
    public string? ImportedProviderId { get; private set; }
    public int ImportedSkillCount { get; private set; }
    
    public ApiImportWindow()
    {
        InitializeComponent();
    }
    
    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlInput.Text.Trim();
        var providerId = ProviderIdInput.Text.Trim().ToLower().Replace(" ", "_");
        
        // Validation
        if (string.IsNullOrEmpty(url) || url == "https://")
        {
            MessageBox.Show("Please enter a valid OpenAPI/Swagger URL.", "Validation", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (string.IsNullOrEmpty(providerId))
        {
            MessageBox.Show("Please enter a provider ID.", "Validation", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        // Show progress
        ImportButton.IsEnabled = false;
        UrlInput.IsEnabled = false;
        ProviderIdInput.IsEnabled = false;
        ProgressPanel.Visibility = Visibility.Visible;
        ResultPanel.Visibility = Visibility.Collapsed;
        
        var progress = new Progress<IngestionProgress>(UpdateProgress);
        
        try
        {
            // Ensure services are initialized
            if (!ApiOrchestrationFactory.IsInitialized)
            {
                ProgressLabel.Text = "Initializing...";
                await ApiOrchestrationFactory.InitializeLocalAsync();
            }
            
            var ingestionService = ApiOrchestrationFactory.GetIngestionService();
            
            var result = await ingestionService.ImportFromUrlAsync(url, providerId, progress);
            
            if (result.Success)
            {
                ImportSuccessful = true;
                ImportedProviderId = providerId;
                ImportedSkillCount = result.SkillCount;
                
                ShowResult(true, 
                    $"Successfully imported {result.SkillCount} skills!",
                    $"Provider: {providerId}");
            }
            else
            {
                ShowResult(false, "Import failed", result.Error ?? "Unknown error");
            }
        }
        catch (Exception ex)
        {
            ShowResult(false, "Import error", ex.Message);
        }
        finally
        {
            ImportButton.IsEnabled = true;
            UrlInput.IsEnabled = true;
            ProviderIdInput.IsEnabled = true;
        }
    }
    
    private void UpdateProgress(IngestionProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressLabel.Text = progress.Stage;
            ProgressBar.Value = progress.Percentage;
            ProgressDetail.Text = progress.Message ?? "";
        });
    }
    
    private void ShowResult(bool success, string message, string detail)
    {
        ProgressPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Visible;
        
        ResultIcon.Text = success ? "✅" : "❌";
        ResultMessage.Text = message;
        ResultDetail.Text = detail;
        
        if (success)
        {
            ImportButton.Content = "Done";
            ImportButton.Click -= ImportButton_Click;
            ImportButton.Click += (s, e) => { DialogResult = true; Close(); };
        }
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}





