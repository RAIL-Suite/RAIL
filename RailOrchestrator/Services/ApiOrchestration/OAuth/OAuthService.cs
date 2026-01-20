using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace WpfRagApp.Services.ApiOrchestration.OAuth;

/// <summary>
/// OAuth 2.0 service that handles the complete OAuth flow:
/// 1. Opens browser for authorization
/// 2. Listens for callback on local port
/// 3. Exchanges authorization code for access token
/// </summary>
public class OAuthService : IDisposable
{
    private readonly HttpClient _httpClient;
    private HttpListener? _listener;
    private const int CallbackPort = 8888;
    private const string CallbackPath = "/callback";
    
    public OAuthService()
    {
        _httpClient = new HttpClient();
    }
    
    /// <summary>
    /// Performs complete OAuth flow and returns tokens.
    /// </summary>
    public async Task<OAuthTokenResult?> AuthorizeAsync(OAuthConfig config, CancellationToken ct = default)
    {
        // Build authorization URL
        var authUrl = BuildAuthorizationUrl(config);
        
        // Start local listener before opening browser
        var codeTask = WaitForAuthorizationCodeAsync(ct);
        
        // Open browser
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = authUrl,
            UseShellExecute = true
        });
        
        // Wait for callback with authorization code
        var code = await codeTask;
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }
        
        // Exchange code for tokens
        return await ExchangeCodeForTokensAsync(config, code);
    }
    
    private string BuildAuthorizationUrl(OAuthConfig config)
    {
        var scopes = string.Join(" ", config.Scopes);
        var redirectUri = $"http://localhost:{CallbackPort}{CallbackPath}";
        
        return $"{config.AuthorizationUrl}?" +
               $"client_id={Uri.EscapeDataString(config.ClientId)}&" +
               $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
               $"response_type=code&" +
               $"scope={Uri.EscapeDataString(scopes)}&" +
               $"access_type=offline&" +
               $"prompt=consent";
    }
    
    private async Task<string?> WaitForAuthorizationCodeAsync(CancellationToken ct)
    {
        var prefix = $"http://localhost:{CallbackPort}/";
        
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _listener.Start();
            
            System.Diagnostics.Debug.WriteLine($"[OAuth] Listening on {prefix}");
            
            // Wait for callback (with timeout)
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromMinutes(5)); // 5 minute timeout
            
            var contextTask = _listener.GetContextAsync();
            var completedTask = await Task.WhenAny(contextTask, Task.Delay(-1, cts.Token));
            
            if (completedTask != contextTask)
            {
                return null; // Timeout or cancelled
            }
            
            var context = await contextTask;
            var request = context.Request;
            var response = context.Response;
            
            // Parse authorization code from query string
            var query = request.Url?.Query ?? "";
            var queryParams = HttpUtility.ParseQueryString(query);
            var code = queryParams["code"];
            var error = queryParams["error"];
            
            // Send response to browser
            string responseHtml;
            if (!string.IsNullOrEmpty(error))
            {
                responseHtml = GetErrorHtml(error, queryParams["error_description"]);
            }
            else if (!string.IsNullOrEmpty(code))
            {
                responseHtml = GetSuccessHtml();
            }
            else
            {
                responseHtml = GetErrorHtml("unknown", "No authorization code received");
            }
            
            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            response.ContentType = "text/html";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length, ct);
            response.Close();
            
            return code;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OAuth] Listener error: {ex.Message}");
            return null;
        }
        finally
        {
            StopListener();
        }
    }
    
    private async Task<OAuthTokenResult?> ExchangeCodeForTokensAsync(OAuthConfig config, string code)
    {
        var redirectUri = $"http://localhost:{CallbackPort}{CallbackPath}";
        
        var requestBody = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", config.ClientId),
            new KeyValuePair<string, string>("client_secret", config.ClientSecret),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("redirect_uri", redirectUri),
            new KeyValuePair<string, string>("grant_type", "authorization_code")
        });
        
        try
        {
            var response = await _httpClient.PostAsync(config.TokenUrl, requestBody);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"[OAuth] Token exchange failed: {content}");
                return null;
            }
            
            var tokenResponse = JsonSerializer.Deserialize<OAuthTokenResponse>(content);
            if (tokenResponse == null) return null;
            
            return new OAuthTokenResult
            {
                AccessToken = tokenResponse.access_token ?? "",
                RefreshToken = tokenResponse.refresh_token,
                ExpiresIn = tokenResponse.expires_in,
                TokenType = tokenResponse.token_type ?? "Bearer",
                Scope = tokenResponse.scope
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OAuth] Token exchange error: {ex.Message}");
            return null;
        }
    }
    
    private void StopListener()
    {
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
        _listener = null;
    }
    
    private string GetSuccessHtml()
    {
        return """
            <!DOCTYPE html>
            <html>
            <head>
                <title>Authorization Successful</title>
                <style>
                    body { font-family: 'Segoe UI', sans-serif; background: #1e1e1e; color: #fff; 
                           display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; }
                    .container { text-align: center; padding: 40px; }
                    .icon { font-size: 64px; margin-bottom: 20px; }
                    h1 { color: #4CAF50; margin-bottom: 10px; }
                    p { color: #888; }
                </style>
            </head>
            <body>
                <div class="container">
                    <div class="icon">✅</div>
                    <h1>Authorization Successful!</h1>
                    <p>You can close this window and return to Rail Orchestrator.</p>
                </div>
            </body>
            </html>
            """;
    }
    
    private string GetErrorHtml(string error, string? description)
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <title>Authorization Failed</title>
    <style>
        body { font-family: 'Segoe UI', sans-serif; background: #1e1e1e; color: #fff; 
               display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; }
        .container { text-align: center; padding: 40px; }
        .icon { font-size: 64px; margin-bottom: 20px; }
        h1 { color: #f44336; margin-bottom: 10px; }
        p { color: #888; }
        .error { color: #ff9800; margin-top: 20px; }
    </style>
</head>
<body>
    <div class='container'>
        <div class='icon'>❌</div>
        <h1>Authorization Failed</h1>
        <p>Please try again or check your OAuth configuration.</p>
        <p class='error'>Error: " + error + "<br/>" + (description ?? "") + @"</p>
    </div>
</body>
</html>";
    }
    
    public void Dispose()
    {
        StopListener();
        _httpClient.Dispose();
    }
}

/// <summary>
/// OAuth configuration for authorization.
/// </summary>
public class OAuthConfig
{
    public required string AuthorizationUrl { get; set; }
    public required string TokenUrl { get; set; }
    public required string ClientId { get; set; }
    public required string ClientSecret { get; set; }
    public List<string> Scopes { get; set; } = new();
}

/// <summary>
/// Result of successful OAuth authorization.
/// </summary>
public class OAuthTokenResult
{
    public required string AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public string? Scope { get; set; }
}

/// <summary>
/// Token response from OAuth provider.
/// </summary>
internal class OAuthTokenResponse
{
    public string? access_token { get; set; }
    public string? refresh_token { get; set; }
    public int expires_in { get; set; }
    public string? token_type { get; set; }
    public string? scope { get; set; }
}





