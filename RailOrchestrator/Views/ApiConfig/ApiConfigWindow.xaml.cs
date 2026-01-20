using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfRagApp.Services.ApiOrchestration;
using WpfRagApp.Services.ApiOrchestration.Models;
using WpfRagApp.Services.Vault;
using WpfRagApp.Services;

namespace WpfRagApp.Views.ApiConfig;

/// <summary>
/// API Configuration Window - manages connection settings for API providers.
/// LOADS AND SAVES TO api.manifest.json in the provider folder.
/// </summary>
public partial class ApiConfigWindow : Window
{
    private readonly string _providerId;
    private readonly string _providerPath;
    private readonly string _manifestPath;
    private ApiManifest? _manifest;
    private readonly IVaultService _vaultService;
    private readonly string _userId = "default";
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    
    public bool ConfigurationSaved { get; private set; }
    
    public ApiConfigWindow(string providerId)
    {
        InitializeComponent();
        
        _providerId = providerId;
        _providerPath = Path.Combine(AssetService.GetDefaultRootPath(), providerId);
        _manifestPath = Path.Combine(_providerPath, "api.manifest.json");
        _vaultService = ApiOrchestrationFactory.GetVaultService();
        
        LoadFromDisk();
    }
    
    /// <summary>
    /// Load configuration from api.manifest.json on disk
    /// </summary>
    private async void LoadFromDisk()
    {
        try
        {
            // Load manifest from disk
            if (File.Exists(_manifestPath))
            {
                var json = await File.ReadAllTextAsync(_manifestPath);
                _manifest = JsonSerializer.Deserialize<ApiManifest>(json, JsonOptions);
            }
            
            if (_manifest == null)
            {
                // Create default manifest
                _manifest = new ApiManifest
                {
                    ManifestVersion = "2.0",
                    Type = "api",
                    Provider = new ProviderInfo { Id = _providerId, Name = _providerId, BaseUrl = "" },
                    Auth = new AuthConfig { Type = "apikey" },
                    RateLimit = new RateLimitConfig()
                };
            }
            
            // Set UI from manifest
            ProviderName.Text = _manifest.Provider.Name;
            Title = $"Configure {ProviderName.Text}";
            
            // Set auth type
            var authType = _manifest.Auth?.Type?.ToLower() ?? "apikey";
            foreach (ComboBoxItem item in AuthTypeCombo.Items)
            {
                if (item.Tag?.ToString() == authType)
                {
                    AuthTypeCombo.SelectedItem = item;
                    break;
                }
            }
            UpdateAuthPanelVisibility(authType);
            
            // Set rate limit values
            RateLimitInput.Text = _manifest.RateLimit?.RequestsPerMinute.ToString() ?? "60";
            BackoffInput.Text = _manifest.RateLimit?.BackoffInitialMs.ToString() ?? "1000";
            MaxBackoffInput.Text = _manifest.RateLimit?.BackoffMaxMs.ToString() ?? "30000";
            
            // Check connection status from Vault
            var status = await _vaultService.GetStatusAsync(_userId, _providerId);
            UpdateStatusIndicator(status);
            
            // Load skill count
            await LoadSkillCount();
            
            // Load OAuth configuration
            if (_manifest?.Auth != null)
            {
                AuthUrlInput.Text = _manifest.Auth.AuthorizationUrl ?? "";
                TokenUrlInput.Text = _manifest.Auth.TokenUrl ?? "";
                ClientIdInput.Text = _manifest.Auth.ClientId ?? "";
                // Load client secret from manifest
                if (!string.IsNullOrEmpty(_manifest.Auth.ClientSecret))
                {
                    ClientSecretInput.Password = _manifest.Auth.ClientSecret;
                }
                if (_manifest.Auth.Scopes?.Count > 0)
                {
                    ScopesInput.Text = string.Join(", ", _manifest.Auth.Scopes);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiConfigWindow] Load error: {ex.Message}");
        }
    }
    
    private async Task LoadSkillCount()
    {
        try
        {
            var skillsDir = Path.Combine(_providerPath, "skills");
            if (Directory.Exists(skillsDir))
            {
                var count = Directory.GetFiles(skillsDir, "*.json").Length;
                SkillCountText.Text = $"{count} skills available";
            }
            
            if (_manifest?.ImportedAt != null && _manifest.ImportedAt > DateTime.MinValue)
            {
                LastSyncText.Text = $"Last sync: {_manifest.ImportedAt:g}";
            }
        }
        catch
        {
            SkillCountText.Text = "Skills not loaded";
        }
    }
    
    private void UpdateStatusIndicator(ConnectionStatus status)
    {
        switch (status)
        {
            case ConnectionStatus.Connected:
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                StatusText.Text = "Connected";
                DisconnectButton.Visibility = Visibility.Visible;
                break;
            case ConnectionStatus.Expired:
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                StatusText.Text = "Token Expired";
                DisconnectButton.Visibility = Visibility.Visible;
                break;
            case ConnectionStatus.Error:
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                StatusText.Text = "Error";
                DisconnectButton.Visibility = Visibility.Collapsed;
                break;
            default:
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(158, 158, 158));
                StatusText.Text = "Disconnected";
                DisconnectButton.Visibility = Visibility.Collapsed;
                break;
        }
    }
    
    private void AuthTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AuthTypeCombo.SelectedItem is not ComboBoxItem selected) return;
        var authType = selected.Tag?.ToString() ?? "apikey";
        UpdateAuthPanelVisibility(authType);
    }
    
    private void UpdateAuthPanelVisibility(string authType)
    {
        // Show API Key panel for apikey and bearer
        ApiKeyPanel.Visibility = authType == "apikey" || authType == "bearer" 
            ? Visibility.Visible 
            : Visibility.Collapsed;
        
        // Show OAuth panel for oauth2
        OAuthPanel.Visibility = authType == "oauth2" 
            ? Visibility.Visible 
            : Visibility.Collapsed;
        
        // IMPORTANT: Hide Connect button when OAuth is selected (uses OAuth button instead)
        ConnectButton.Visibility = authType != "oauth2" 
            ? Visibility.Visible 
            : Visibility.Collapsed;
        
        // Show OAuth config panel only for OAuth2
        if (OAuthConfigPanel != null)
        {
            OAuthConfigPanel.Visibility = authType == "oauth2" 
                ? Visibility.Visible 
                : Visibility.Collapsed;
        }
    }
    
    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var authType = (AuthTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "apikey";
        
        try
        {
            ConnectButton.IsEnabled = false;
            ConnectButton.Content = "Connecting...";
            
            var apiKey = ApiKeyInput.Password.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("Please enter an API key.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            await _vaultService.SaveApiKeyAsync(_userId, _providerId, new ApiKeyCredentials
            {
                Key = apiKey,
                HeaderName = _manifest?.Auth?.HeaderName ?? "Authorization",
                Prefix = authType == "bearer" ? "Bearer" : null
            });
            
            UpdateStatusIndicator(ConnectionStatus.Connected);
            MessageBox.Show("API Key saved successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Connection failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ConnectButton.IsEnabled = true;
            ConnectButton.Content = "Connect";
        }
    }
    
    private async void OAuthButton_Click(object sender, RoutedEventArgs e)
    {
        // Check if OAuth configuration is complete
        if (_manifest?.Auth == null || 
            string.IsNullOrEmpty(_manifest.Auth.AuthorizationUrl) || 
            string.IsNullOrEmpty(_manifest.Auth.TokenUrl) ||
            string.IsNullOrEmpty(_manifest.Auth.ClientId) ||
            string.IsNullOrEmpty(_manifest.Auth.ClientSecret))
        {
            MessageBox.Show(
                "OAuth configuration not complete.\n\n" +
                "Please configure the following in Advanced Settings:\n" +
                "• Authorization URL\n" +
                "• Token URL\n" +
                "• Client ID\n" +
                "• Client Secret\n" +
                "• Scopes",
                "OAuth Setup Required", 
                MessageBoxButton.OK, 
                MessageBoxImage.Information);
            return;
        }
        
        try
        {
            OAuthButton.IsEnabled = false;
            OAuthButton.Content = "🔄 Opening browser...";
            
            // Use automatic OAuth service
            using var oauthService = new WpfRagApp.Services.ApiOrchestration.OAuth.OAuthService();
            
            var config = new WpfRagApp.Services.ApiOrchestration.OAuth.OAuthConfig
            {
                AuthorizationUrl = _manifest.Auth.AuthorizationUrl,
                TokenUrl = _manifest.Auth.TokenUrl,
                ClientId = _manifest.Auth.ClientId,
                ClientSecret = _manifest.Auth.ClientSecret,
                Scopes = _manifest.Auth.Scopes ?? new List<string>()
            };
            
            OAuthButton.Content = "🔄 Waiting for authorization...";
            
            // This will open browser, wait for callback, and exchange code for token
            var result = await oauthService.AuthorizeAsync(config);
            
            if (result != null && !string.IsNullOrEmpty(result.AccessToken))
            {
                // Save tokens to vault
                await _vaultService.SaveOAuth2Async(_userId, _providerId, new OAuth2Credentials
                {
                    AccessToken = result.AccessToken,
                    RefreshToken = result.RefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(result.ExpiresIn)
                });
                
                UpdateStatusIndicator(ConnectionStatus.Connected);
                MessageBox.Show("OAuth connected successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("OAuth authorization was cancelled or failed.", "Cancelled", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"OAuth failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            OAuthButton.IsEnabled = true;
            OAuthButton.Content = "🔐 Connect with OAuth";
        }
    }
    
    private async void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            $"Are you sure you want to disconnect from {ProviderName.Text}?",
            "Confirm Disconnect",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        
        if (result == MessageBoxResult.Yes)
        {
            await _vaultService.DeleteAsync(_userId, _providerId);
            UpdateStatusIndicator(ConnectionStatus.Disconnected);
            ApiKeyInput.Password = "";
        }
    }
    
    /// <summary>
    /// SAVE CONFIGURATION TO api.manifest.json
    /// </summary>
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Update manifest with UI values
            if (_manifest == null)
            {
                _manifest = new ApiManifest
                {
                    ManifestVersion = "2.0",
                    Type = "api",
                    Provider = new ProviderInfo { Id = _providerId, Name = _providerId, BaseUrl = "" },
                    Auth = new AuthConfig { Type = "apikey" }
                };
            }
            
            // Update auth type
            var authType = (AuthTypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "apikey";
            _manifest.Auth.Type = authType;
            
            // Update OAuth configuration
            if (authType == "oauth2")
            {
                _manifest.Auth.AuthorizationUrl = AuthUrlInput.Text?.Trim();
                _manifest.Auth.TokenUrl = TokenUrlInput.Text?.Trim();
                _manifest.Auth.ClientId = ClientIdInput.Text?.Trim();
                // Parse scopes (comma or space separated)
                var scopesText = ScopesInput.Text?.Trim() ?? "";
                _manifest.Auth.Scopes = scopesText
                    .Split(new[] { ',', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
                
                // Save client secret directly to manifest
                var secret = ClientSecretInput.Password?.Trim();
                if (!string.IsNullOrEmpty(secret))
                {
                    _manifest.Auth.ClientSecret = secret;
                }
            }
            
            // Update rate limits
            if (int.TryParse(RateLimitInput.Text, out var rpm))
                _manifest.RateLimit.RequestsPerMinute = rpm;
            if (int.TryParse(BackoffInput.Text, out var backoff))
                _manifest.RateLimit.BackoffInitialMs = backoff;
            if (int.TryParse(MaxBackoffInput.Text, out var maxBackoff))
                _manifest.RateLimit.BackoffMaxMs = maxBackoff;
            
            // SAVE TO DISK
            var json = JsonSerializer.Serialize(_manifest, JsonOptions);
            await File.WriteAllTextAsync(_manifestPath, json);
            
            ConfigurationSaved = true;
            //MessageBox.Show("Configuration saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}





