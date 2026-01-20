using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RailStudio.Models
{
    public class RailPackage
    {
        [JsonPropertyName("package_name")]
        public string PackageName { get; set; } = string.Empty;

        [JsonPropertyName("function_mapped")]
        public string FunctionName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>();
    }
}




