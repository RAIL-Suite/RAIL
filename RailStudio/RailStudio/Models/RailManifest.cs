using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace RailStudio.Models
{
    public class RailManifest
    {
        [JsonPropertyName("language")]
        public string Language { get; set; } = string.Empty;

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("entry_point")]
        public string EntryPoint { get; set; } = string.Empty;

        [JsonPropertyName("metadata")]
        public RailMetadata Metadata { get; set; } = new();

        [JsonPropertyName("tools")]
        public List<RailTool> Tools { get; set; } = new();

        [JsonPropertyName("dependencies")]
        public List<string> Dependencies { get; set; } = new();
    }

    public class RailMetadata
    {
        [JsonPropertyName("artifact_id")]
        public string ArtifactId { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class RailTool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Full qualified class name containing the method.
        /// Used for precise method lookup at runtime to avoid ambiguity.
        /// </summary>
        [JsonPropertyName("class")]
        public string ClassName { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public Dictionary<string, object> InputSchema { get; set; } = new();
        
        /// <summary>
        /// Parsed hierarchical parameters for UI display.
        /// </summary>
        [JsonIgnore]
        public ObservableCollection<ParameterInfo> ParsedParameters
        {
            get
            {
                var result = new ObservableCollection<ParameterInfo>();
                
                try
                {
                    if (InputSchema == null || InputSchema.Count == 0)
                        return result;
                    
                    // Navigate to properties object
                    if (!InputSchema.TryGetValue("properties", out var propsObj))
                        return result;
                    
                    if (propsObj is not System.Text.Json.JsonElement propsElement || 
                        propsElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                        return result;
                    
                    // Get required array
                    var requiredParams = new HashSet<string>();
                    if (InputSchema.TryGetValue("required", out var reqObj) && 
                        reqObj is System.Text.Json.JsonElement reqElement &&
                        reqElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var item in reqElement.EnumerateArray())
                        {
                            if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                                requiredParams.Add(item.GetString() ?? "");
                        }
                    }
                    
                    // Parse each parameter
                    foreach (var prop in propsElement.EnumerateObject())
                    {
                        var paramInfo = ParseParameter(prop.Name, prop.Value, requiredParams.Contains(prop.Name));
                        if (paramInfo != null)
                            result.Add(paramInfo);
                    }
                }
                catch
                {
                    // Return empty on parse error
                }
                
                return result;
            }
        }
        
        /// <summary>
        /// Legacy string-formatted parameters for backward compatibility.
        /// </summary>
        [JsonIgnore]
        public string ParametersFormatted
        {
            get
            {
                var parsed = ParsedParameters;
                if (parsed.Count == 0)
                    return "(none)";
                
                return string.Join(", ", parsed.Select(p => $"{p.Name}: {p.TypeDisplay}"));
            }
        }
        
        /// <summary>
        /// Recursively parses a parameter from JSON Schema.
        /// </summary>
        private ParameterInfo? ParseParameter(string name, System.Text.Json.JsonElement schema, bool isRequired)
        {
            try
            {
                var param = new ParameterInfo
                {
                    Name = name,
                    IsRequired = isRequired
                };
                
                // Get type
                if (schema.TryGetProperty("type", out var typeElem))
                {
                    param.JsonType = typeElem.GetString() ?? "OBJECT";
                }
                
                // Get typeName for C# display
                if (schema.TryGetProperty("typeName", out var typeNameElem))
                {
                    param.TypeDisplay = typeNameElem.GetString() ?? param.JsonType;
                }
                else
                {
                    // Map JSON type to C# type
                    param.TypeDisplay = param.JsonType switch
                    {
                        "INTEGER" => "int",
                        "NUMBER" => "double",
                        "STRING" => "string",
                        "BOOLEAN" => "bool",
                        "ARRAY" => "List",
                        "OBJECT" => "object",
                        _ => param.JsonType
                    };
                }
                
                // Handle nested properties (complex types)
                if (param.JsonType == "OBJECT" && schema.TryGetProperty("properties", out var propsElem))
                {
                    param.IsComplex = true;
                    
                    if (propsElem.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        // Get nested required array
                        var nestedRequired = new HashSet<string>();
                        if (schema.TryGetProperty("required", out var reqElem) &&
                            reqElem.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var req in reqElem.EnumerateArray())
                            {
                                if (req.ValueKind == System.Text.Json.JsonValueKind.String)
                                    nestedRequired.Add(req.GetString() ?? "");
                            }
                        }
                        
                        // Parse nested properties
                        foreach (var nestedProp in propsElem.EnumerateObject())
                        {
                            var nestedParam = ParseParameter(
                                nestedProp.Name, 
                                nestedProp.Value, 
                                nestedRequired.Contains(nestedProp.Name));
                            
                            if (nestedParam != null)
                                param.Properties.Add(nestedParam);
                        }
                    }
                }
                
                // Handle arrays
                if (param.JsonType == "ARRAY" && schema.TryGetProperty("items", out var itemsElem))
                {
                    var itemType = "object";
                    if (itemsElem.TryGetProperty("typeName", out var itemTypeElem))
                    {
                        itemType = itemTypeElem.GetString() ?? "object";
                    }
                    
                    param.TypeDisplay = $"List<{itemType}>";
                }
                
                return param;
            }
            catch
            {
                return null;
            }
        }
    }
    
    /// <summary>
    /// Hierarchical parameter information for UI tree display.
    /// </summary>
    public class ParameterInfo
    {
        public string Name { get; set; } = "";
        public string JsonType { get; set; } = "OBJECT";
        public string TypeDisplay { get; set; } = "object";
        public bool IsRequired { get; set; }
        public bool IsComplex { get; set; }
        public ObservableCollection<ParameterInfo> Properties { get; set; } = new();
    }
}




