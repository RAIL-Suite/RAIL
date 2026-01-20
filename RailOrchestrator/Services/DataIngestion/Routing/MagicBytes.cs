using System.IO.Compression;

namespace WpfRagApp.Services.DataIngestion.Routing;

/// <summary>
/// Magic bytes signatures for file type detection.
/// </summary>
public static class MagicBytes
{
    // ZIP-based formats (XLSX, DOCX)
    public static readonly byte[] ZIP = { 0x50, 0x4B, 0x03, 0x04 };
    
    // PDF
    public static readonly byte[] PDF = { 0x25, 0x50, 0x44, 0x46 }; // %PDF
    
    // Old Excel (XLS)
    public static readonly byte[] XLS = { 0xD0, 0xCF, 0x11, 0xE0 };
    
    /// <summary>
    /// Check if file starts with given magic bytes.
    /// </summary>
    public static bool MatchesSignature(string filePath, byte[] signature)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < signature.Length)
                return false;
            
            var buffer = new byte[signature.Length];
            fs.Read(buffer, 0, signature.Length);
            
            for (int i = 0; i < signature.Length; i++)
            {
                if (buffer[i] != signature[i])
                    return false;
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Check if ZIP file contains Office Open XML markers (XLSX/DOCX).
    /// </summary>
    public static bool IsOfficeOpenXml(string filePath)
    {
        if (!MatchesSignature(filePath, ZIP))
            return false;
        
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);
            return archive.GetEntry("[Content_Types].xml") != null;
        }
        catch
        {
            return false;
        }
    }
}





