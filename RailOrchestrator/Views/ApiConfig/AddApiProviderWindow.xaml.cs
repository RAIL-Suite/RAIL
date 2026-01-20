using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfRagApp.Services.ApiOrchestration;
using WpfRagApp.Services.ApiOrchestration.Ingestion;
using WpfRagApp.Services.ApiOrchestration.Models;
using WpfRagApp.Services;

namespace WpfRagApp.Views.ApiConfig;

/// <summary>
/// Dialog for adding a new API provider.
/// Creates folder, manifest, AND automatically imports skills from OpenAPI spec.
/// </summary>
public partial class AddApiProviderWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public string? CreatedProviderId { get; private set; }
    
    public AddApiProviderWindow()
    {
        InitializeComponent();
        ProviderNameInput.Focus();
    }
    
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
    
    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Validate provider name
            var providerName = ProviderNameInput.Text?.Trim();
            if (string.IsNullOrEmpty(providerName))
            {
                ShowError("Provider name is required.");
                return;
            }
            
            // Validate OpenAPI URL
            var openApiUrl = OpenApiUrlInput.Text?.Trim();
            if (string.IsNullOrEmpty(openApiUrl))
            {
                ShowError("OpenAPI Specification URL is required.");
                return;
            }
            
            if (!Uri.TryCreate(openApiUrl, UriKind.Absolute, out _))
            {
                ShowError("OpenAPI URL is not valid.");
                return;
            }
            
            // Sanitize provider ID (remove invalid chars but keep case)
            var providerId = SanitizeProviderId(providerName);
            if (string.IsNullOrEmpty(providerId))
            {
                ShowError("Provider name contains only invalid characters.");
                return;
            }
            
            // Check if already exists
            var providerPath = Path.Combine(AssetService.GetDefaultRootPath(), providerId);
            if (Directory.Exists(providerPath))
            {
                ShowError($"Provider '{providerId}' already exists.");
                return;
            }
            
            // Disable UI during import
            SetUIEnabled(false);
            ShowProgress("Creating provider...");
            
            // Get display name
            var displayName = string.IsNullOrWhiteSpace(DisplayNameInput.Text) 
                ? providerName 
                : DisplayNameInput.Text.Trim();
            var authType = (AuthTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "apikey";
            var baseUrl = BaseUrlInput.Text?.Trim() ?? "";
            
            // Create folder structure first
            Directory.CreateDirectory(providerPath);
            Directory.CreateDirectory(Path.Combine(providerPath, "skills"));
            
            // Create initial manifest (will be updated by ingestion)
            var manifest = new ApiManifest
            {
                ManifestVersion = "2.0",
                Type = "api",
                Provider = new ProviderInfo
                {
                    Id = providerId,
                    Name = displayName,
                    BaseUrl = baseUrl
                },
                Auth = new AuthConfig
                {
                    Type = authType,
                    HeaderName = authType switch
                    {
                        "apikey" => "X-API-Key",
                        "bearer" => "Authorization",
                        _ => null
                    },
                    HeaderPrefix = authType == "bearer" ? "Bearer" : null
                },
                RateLimit = new RateLimitConfig
                {
                    RequestsPerMinute = 60,
                    BackoffInitialMs = 1000,
                    MaxRetries = 3
                },
                SkillFiles = new List<string>(),
                ImportedAt = DateTime.UtcNow
            };
            
            // Save initial manifest
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(Path.Combine(providerPath, "api.manifest.json"), manifestJson);
            
            // === AUTOMATIC INGESTION ===
            ShowProgress("Downloading OpenAPI specification...");
            
            var ingestionService = ApiOrchestrationFactory.GetIngestionService();
            var progress = new Progress<IngestionProgress>(p =>
            {
                Dispatcher.Invoke(() => ShowProgress($"{p.Stage}: {p.Message}"));
            });
            
            var result = await ingestionService.ImportFromUrlAsync(openApiUrl, providerId, progress);
            
            if (!result.Success)
            {
                // Clean up on failure
                try { Directory.Delete(providerPath, true); } catch { }
                ShowError($"Import failed: {result.Error}");
                SetUIEnabled(true);
                return;
            }
            
            // Success!
            ShowSuccess($"✅ Imported {result.SkillCount} skills for {result.ProviderId}");
            await Task.Delay(1500); // Show success message briefly
            
            CreatedProviderId = providerId;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
            SetUIEnabled(true);
        }
    }
    
    private string SanitizeProviderId(string input)
    {
        var chars = input.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '-').ToArray();
        return new string(chars);
    }
    
    private void SetUIEnabled(bool enabled)
    {
        ProviderNameInput.IsEnabled = enabled;
        OpenApiUrlInput.IsEnabled = enabled;
        DisplayNameInput.IsEnabled = enabled;
        AuthTypeCombo.IsEnabled = enabled;
        BaseUrlInput.IsEnabled = enabled;
        CreateButton.IsEnabled = enabled;
        CancelButton.IsEnabled = enabled;
        
        ProgressBar.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        ProgressText.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
    }
    
    private void ShowProgress(string message)
    {
        StatusText.Text = "";
        ProgressText.Text = message;
        ProgressText.Visibility = Visibility.Visible;
        ProgressBar.Visibility = Visibility.Visible;
    }
    
    private void ShowError(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6B6B")!);
        ProgressBar.Visibility = Visibility.Collapsed;
        ProgressText.Visibility = Visibility.Collapsed;
    }
    
    private void ShowSuccess(string message)
    {
        StatusText.Text = message;
        StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5E9B8A")!);
        ProgressBar.Visibility = Visibility.Collapsed;
        ProgressText.Visibility = Visibility.Collapsed;
    }
}





