using Microsoft.Data.Sqlite;
using Dapper;

namespace WpfRagApp.Services.Vault;

/// <summary>
/// Secure credential vault service.
/// Uses SQLite for storage and AES-256-GCM for encryption.
/// </summary>
public class VaultService : IVaultService, IDisposable
{
    private readonly string _dbPath;
    private readonly EncryptionService _encryption;
    private readonly string _connectionString;
    
    public VaultService(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Rail", "vault", "credentials.db"
        );
        
        // Ensure directory exists
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        
        _connectionString = $"Data Source={_dbPath}";
        _encryption = new EncryptionService();
        _encryption.Initialize();
        
        InitializeDatabase();
    }
    
    private void InitializeDatabase()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        
        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS vault_credentials (
                id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                provider_id TEXT NOT NULL,
                auth_type TEXT NOT NULL,
                encrypted_data BLOB NOT NULL,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT DEFAULT CURRENT_TIMESTAMP,
                expires_at TEXT,
                status TEXT DEFAULT 'Connected',
                UNIQUE(user_id, provider_id)
            );
            
            CREATE TABLE IF NOT EXISTS vault_audit (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                user_id TEXT NOT NULL,
                provider_id TEXT NOT NULL,
                action TEXT NOT NULL,
                timestamp TEXT DEFAULT CURRENT_TIMESTAMP
            );
            
            CREATE INDEX IF NOT EXISTS idx_credentials_user_provider 
            ON vault_credentials(user_id, provider_id);
        ");
    }
    
    #region Save Credentials
    
    public async Task SaveOAuth2Async(string userId, string providerId, OAuth2Credentials credentials)
    {
        var encrypted = _encryption.EncryptObject(credentials);
        await SaveCredentialAsync(userId, providerId, "oauth2", encrypted, credentials.ExpiresAt);
    }
    
    public async Task SaveApiKeyAsync(string userId, string providerId, ApiKeyCredentials credentials)
    {
        var encrypted = _encryption.EncryptObject(credentials);
        await SaveCredentialAsync(userId, providerId, "apikey", encrypted, null);
    }
    
    public async Task SaveBasicAuthAsync(string userId, string providerId, BasicAuthCredentials credentials)
    {
        var encrypted = _encryption.EncryptObject(credentials);
        await SaveCredentialAsync(userId, providerId, "basic", encrypted, null);
    }
    
    private async Task SaveCredentialAsync(string userId, string providerId, string authType, 
        byte[] encryptedData, DateTime? expiresAt)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        var id = $"{userId}:{providerId}";
        
        await conn.ExecuteAsync(@"
            INSERT INTO vault_credentials (id, user_id, provider_id, auth_type, encrypted_data, expires_at, updated_at, status)
            VALUES (@id, @userId, @providerId, @authType, @encryptedData, @expiresAt, datetime('now'), 'Connected')
            ON CONFLICT(user_id, provider_id) DO UPDATE SET
                auth_type = @authType,
                encrypted_data = @encryptedData,
                expires_at = @expiresAt,
                updated_at = datetime('now'),
                status = 'Connected'
        ", new { id, userId, providerId, authType, encryptedData, expiresAt = expiresAt?.ToString("O") });
        
        await AuditLogAsync(userId, providerId, "write");
    }
    
    #endregion
    
    #region Get Credentials
    
    public async Task<CredentialEntry?> GetCredentialsAsync(string userId, string providerId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        var row = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT id, user_id, provider_id, auth_type, encrypted_data, created_at, updated_at, expires_at, status
            FROM vault_credentials
            WHERE user_id = @userId AND provider_id = @providerId
        ", new { userId, providerId });
        
        if (row == null) return null;
        
        await AuditLogAsync(userId, providerId, "read");
        
        return new CredentialEntry
        {
            Id = row.id,
            UserId = row.user_id,
            ProviderId = row.provider_id,
            AuthType = row.auth_type,
            EncryptedData = row.encrypted_data,
            CreatedAt = DateTime.Parse(row.created_at),
            UpdatedAt = DateTime.Parse(row.updated_at),
            ExpiresAt = row.expires_at != null ? DateTime.Parse(row.expires_at) : null,
            Status = Enum.Parse<ConnectionStatus>(row.status)
        };
    }
    
    public async Task<OAuth2Credentials?> GetOAuth2Async(string userId, string providerId)
    {
        var entry = await GetCredentialsAsync(userId, providerId);
        if (entry?.EncryptedData == null || entry.AuthType != "oauth2") return null;
        
        var credentials = _encryption.DecryptObject<OAuth2Credentials>(entry.EncryptedData);
        
        // Check if token needs refresh
        if (credentials?.IsNearExpiry == true && credentials.RefreshToken != null)
        {
            return await RefreshOAuth2Async(userId, providerId);
        }
        
        return credentials;
    }
    
    public async Task<ApiKeyCredentials?> GetApiKeyAsync(string userId, string providerId)
    {
        var entry = await GetCredentialsAsync(userId, providerId);
        if (entry?.EncryptedData == null || entry.AuthType != "apikey") return null;
        
        return _encryption.DecryptObject<ApiKeyCredentials>(entry.EncryptedData);
    }
    
    #endregion
    
    #region Refresh & Token Management
    
    public async Task<OAuth2Credentials?> RefreshOAuth2Async(string userId, string providerId)
    {
        var entry = await GetCredentialsAsync(userId, providerId);
        if (entry?.EncryptedData == null) return null;
        
        var credentials = _encryption.DecryptObject<OAuth2Credentials>(entry.EncryptedData);
        if (credentials?.RefreshToken == null) return null;
        
        // TODO: Implement actual token refresh via HTTP
        // This will be implemented when we add the OAuth flow
        // For now, return the existing credentials
        
        await AuditLogAsync(userId, providerId, "refresh");
        return credentials;
    }
    
    #endregion
    
    #region Status & Management
    
    public async Task DeleteAsync(string userId, string providerId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        await conn.ExecuteAsync(@"
            DELETE FROM vault_credentials
            WHERE user_id = @userId AND provider_id = @providerId
        ", new { userId, providerId });
        
        await AuditLogAsync(userId, providerId, "revoke");
    }
    
    public async Task<bool> IsConnectedAsync(string userId, string providerId)
    {
        var status = await GetStatusAsync(userId, providerId);
        return status == ConnectionStatus.Connected;
    }
    
    public async Task<ConnectionStatus> GetStatusAsync(string userId, string providerId)
    {
        var entry = await GetCredentialsAsync(userId, providerId);
        if (entry == null) return ConnectionStatus.Disconnected;
        
        if (entry.AuthType == "oauth2" && entry.ExpiresAt.HasValue)
        {
            if (DateTime.UtcNow > entry.ExpiresAt.Value)
            {
                return ConnectionStatus.Expired;
            }
        }
        
        return entry.Status;
    }
    
    public async Task<List<string>> GetConnectedProvidersAsync(string userId)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        var providers = await conn.QueryAsync<string>(@"
            SELECT provider_id FROM vault_credentials
            WHERE user_id = @userId AND status = 'Connected'
        ", new { userId });
        
        return providers.ToList();
    }
    
    #endregion
    
    #region Audit
    
    private async Task AuditLogAsync(string userId, string providerId, string action)
    {
        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        
        await conn.ExecuteAsync(@"
            INSERT INTO vault_audit (user_id, provider_id, action)
            VALUES (@userId, @providerId, @action)
        ", new { userId, providerId, action });
    }
    
    #endregion
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}





