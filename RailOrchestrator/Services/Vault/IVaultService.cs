namespace WpfRagApp.Services.Vault;

/// <summary>
/// Interface for credential vault operations.
/// Provides secure storage and retrieval of API credentials.
/// </summary>
public interface IVaultService
{
    /// <summary>
    /// Store OAuth2 credentials for a provider.
    /// </summary>
    Task SaveOAuth2Async(string userId, string providerId, OAuth2Credentials credentials);
    
    /// <summary>
    /// Store API Key credentials for a provider.
    /// </summary>
    Task SaveApiKeyAsync(string userId, string providerId, ApiKeyCredentials credentials);
    
    /// <summary>
    /// Store Basic Auth credentials for a provider.
    /// </summary>
    Task SaveBasicAuthAsync(string userId, string providerId, BasicAuthCredentials credentials);
    
    /// <summary>
    /// Get credentials for a provider.
    /// Automatically refreshes OAuth2 tokens if near expiry.
    /// </summary>
    Task<CredentialEntry?> GetCredentialsAsync(string userId, string providerId);
    
    /// <summary>
    /// Get decrypted OAuth2 credentials.
    /// </summary>
    Task<OAuth2Credentials?> GetOAuth2Async(string userId, string providerId);
    
    /// <summary>
    /// Get decrypted API Key credentials.
    /// </summary>
    Task<ApiKeyCredentials?> GetApiKeyAsync(string userId, string providerId);
    
    /// <summary>
    /// Refresh OAuth2 token.
    /// </summary>
    Task<OAuth2Credentials?> RefreshOAuth2Async(string userId, string providerId);
    
    /// <summary>
    /// Delete credentials for a provider (disconnect).
    /// </summary>
    Task DeleteAsync(string userId, string providerId);
    
    /// <summary>
    /// Check if provider is connected.
    /// </summary>
    Task<bool> IsConnectedAsync(string userId, string providerId);
    
    /// <summary>
    /// Get connection status for a provider.
    /// </summary>
    Task<ConnectionStatus> GetStatusAsync(string userId, string providerId);
    
    /// <summary>
    /// List all connected providers for a user.
    /// </summary>
    Task<List<string>> GetConnectedProvidersAsync(string userId);
}





