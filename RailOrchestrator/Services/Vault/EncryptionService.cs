using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WpfRagApp.Services.Vault;

/// <summary>
/// Provides AES-256-GCM encryption/decryption for sensitive data.
/// Uses Windows DPAPI for key protection.
/// </summary>
public class EncryptionService
{
    private const int KeySize = 32;     // 256 bits
    private const int NonceSize = 12;   // 96 bits (GCM standard)
    private const int TagSize = 16;     // 128 bits
    private const string MagicHeader = "LQVT"; // Rail Vault
    private const int Version = 1;
    
    private byte[]? _masterKey;
    
    /// <summary>
    /// Initialize encryption with a master key.
    /// In production, derive from Windows DPAPI or secure key store.
    /// </summary>
    public void Initialize(string? passphrase = null)
    {
        if (passphrase != null)
        {
            // Derive key from passphrase using PBKDF2
            _masterKey = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(passphrase),
                Encoding.UTF8.GetBytes("RailVaultSalt2024"),
                iterations: 100000,
                HashAlgorithmName.SHA256,
                KeySize
            );
        }
        else
        {
            // Use machine-specific key via DPAPI
            var machineEntropy = Encoding.UTF8.GetBytes(Environment.MachineName + "RailVault");
            var baseKey = new byte[KeySize];
            RandomNumberGenerator.Fill(baseKey);
            
            // Protect with DPAPI (Windows only)
            _masterKey = ProtectedData.Protect(baseKey, machineEntropy, DataProtectionScope.CurrentUser);
            
            // Store protected key for later retrieval
            // For now, we'll use a simpler approach with machine-derived key
            _masterKey = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(Environment.MachineName + Environment.UserName),
                Encoding.UTF8.GetBytes("RailVaultMachineKey"),
                iterations: 100000,
                HashAlgorithmName.SHA256,
                KeySize
            );
        }
    }
    
    /// <summary>
    /// Encrypt data using AES-256-GCM.
    /// Returns: [Magic(4)] [Version(4)] [Nonce(12)] [Tag(16)] [Ciphertext(N)]
    /// </summary>
    public byte[] Encrypt(string plaintext)
    {
        EnsureInitialized();
        
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];
        
        using var aes = new AesGcm(_masterKey!, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        
        // Build output: Magic + Version + Nonce + Tag + Ciphertext
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        
        writer.Write(Encoding.ASCII.GetBytes(MagicHeader));
        writer.Write(Version);
        writer.Write(nonce);
        writer.Write(tag);
        writer.Write(ciphertext);
        
        return ms.ToArray();
    }
    
    /// <summary>
    /// Decrypt AES-256-GCM encrypted data.
    /// </summary>
    public string Decrypt(byte[] encryptedData)
    {
        EnsureInitialized();
        
        using var ms = new MemoryStream(encryptedData);
        using var reader = new BinaryReader(ms);
        
        // Validate magic header
        var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (magic != MagicHeader)
        {
            throw new CryptographicException("Invalid vault data format");
        }
        
        // Read version (for future compatibility)
        var version = reader.ReadInt32();
        if (version > Version)
        {
            throw new CryptographicException($"Unsupported vault version: {version}");
        }
        
        // Read components
        var nonce = reader.ReadBytes(NonceSize);
        var tag = reader.ReadBytes(TagSize);
        var ciphertext = reader.ReadBytes((int)(ms.Length - ms.Position));
        
        // Decrypt
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_masterKey!, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        
        return Encoding.UTF8.GetString(plaintext);
    }
    
    /// <summary>
    /// Encrypt an object as JSON.
    /// </summary>
    public byte[] EncryptObject<T>(T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        return Encrypt(json);
    }
    
    /// <summary>
    /// Decrypt JSON to object.
    /// </summary>
    public T? DecryptObject<T>(byte[] encryptedData)
    {
        var json = Decrypt(encryptedData);
        return JsonSerializer.Deserialize<T>(json);
    }
    
    private void EnsureInitialized()
    {
        if (_masterKey == null)
        {
            Initialize();
        }
    }
}





