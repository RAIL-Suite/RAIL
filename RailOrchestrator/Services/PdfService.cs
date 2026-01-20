using UglyToad.PdfPig;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace WpfRagApp.Services
{
    public class PdfService
    {
        public IEnumerable<(string FilePath, string Text)> GetFileTextStream(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                yield break;
            }

            foreach (var filePath in Directory.GetFiles(directoryPath, "*.pdf"))
            {
                yield return (filePath, ExtractTextFromPdf(filePath));
            }
        }

        private string ExtractTextFromPdf(string filePath)
        {
            try
            {
                // For very large files, we might want to yield pages instead of full text
                // But for RAG chunking, we often need context across pages.
                // A compromise is to read the whole text but process files one by one (which we do).
                // If files are TRULY massive (GBs), we would need to stream pages.
                // Given "Enterprise" usually means "Lots of files" or "Big manuals", 
                // reading one file at a time is usually okay if we don't hold them all in memory.
                
                using (var pdf = PdfDocument.Open(filePath))
                {
                    var sb = new StringBuilder();
                    foreach (var page in pdf.GetPages())
                    {
                        sb.Append(page.Text);
                        sb.Append(" ");
                    }
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error reading PDF {filePath}", ex);
                return string.Empty;
            }
        }

        public IEnumerable<string> ChunkText(string text, int chunkSize = 1000, int overlap = 100)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                yield break;
            }

            for (int i = 0; i < text.Length; i += (chunkSize - overlap))
            {
                if (i + chunkSize > text.Length)
                {
                    yield return text.Substring(i);
                    break;
                }
                else
                {
                    yield return text.Substring(i, chunkSize);
                }
            }
        }
    }
}





