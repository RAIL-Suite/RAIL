using System;
using System.Security.Cryptography;
using System.Text;

namespace RailFactory.Core;

/// <summary>
/// Provides deterministic hashing for cross-process name generation.
/// Uses SHA256 to ensure consistent hash values across different processes and sessions.
/// 
/// Critical: string.GetHashCode() is NON-DETERMINISTIC in .NET Core/5+ for security reasons.
/// This class provides a deterministic alternative for IPC scenarios.
/// </summary>
internal static class DeterministicHash
{
    /// <summary>
    /// Generates a deterministic 8-character hex hash from a string.
    /// Same input always produces same output, across processes and machines.
    /// </summary>
    /// <param name="input">String to hash (e.g., "CTest")</param>
    /// <returns>8-character hex string (e.g., "A1B2C3D4")</returns>
    public static string GetHash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "00000000";
            
        using var sha256 = SHA256.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(inputBytes);
        
        // Take first 4 bytes (32 bits) and convert to hex
        // This gives us 8 hex characters, same format as GetHashCode():X
        return BitConverter.ToString(hashBytes, 0, 4).Replace("-", "");
    }
}



