using System.Text.Json.Serialization;

namespace RailStudio.Models
{
    public class AppSettings
    {
        public string RuntimePath { get; set; } = string.Empty;
        public string RailEnginePath { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public RailFactory.Core.ScanOptions ScanOptions { get; set; } = new RailFactory.Core.ScanOptions();
    }
}




