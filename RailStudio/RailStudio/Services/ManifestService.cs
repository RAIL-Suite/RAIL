using RailStudio.Models;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace RailStudio.Services
{
    public interface IManifestService
    {
        Task<RailManifest?> LoadManifestAsync(string path);
        Task SaveManifestAsync(string path, RailManifest manifest);
        Task<bool> DeleteToolAsync(string path, string toolName, int toolIndex, string? className = null);
    }

    public class ManifestService : IManifestService
    {
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task<RailManifest?> LoadManifestAsync(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                // Use FileShare.ReadWrite to allow concurrent write access
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var result = await JsonSerializer.DeserializeAsync<RailManifest>(stream);
                return result;
            }
            catch
            {
                return null;
            }
        }

        public async Task SaveManifestAsync(string path, RailManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            try
            {
                using var stream = File.Create(path);
                await JsonSerializer.SerializeAsync(stream, manifest, _jsonOptions);
            }
            catch (Exception ex)
            {
                throw new IOException($"Failed to save manifest to {path}", ex);
            }
        }

        public async Task<bool> DeleteToolAsync(string path, string toolName, int originalIndex, string? className = null)
        {
            try 
            {
                if (!File.Exists(path)) return false;

                var jsonContent = await File.ReadAllTextAsync(path);
                var root = System.Text.Json.Nodes.JsonNode.Parse(jsonContent);

                if (root == null) return false;

                // Check if this is a composite manifest
                bool isComposite = false;
                if (root is System.Text.Json.Nodes.JsonObject rootObj)
                {
                    isComposite = rootObj.ContainsKey("modules") || rootObj.ContainsKey("Modules");
                }

                bool deleted = false;
                if (isComposite)
                {
                    deleted = DeleteFromComposite(root, toolName, className);
                }
                else
                {
                    deleted = DeleteFromSingle(root, toolName, className, originalIndex);
                }

                if (deleted)
                {
                    var options = new JsonSerializerOptions { WriteIndented = true };
                    await File.WriteAllTextAsync(path, root.ToJsonString(options));
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting tool: {ex.Message}");
                return false;
            }
        }

        private bool DeleteFromSingle(System.Text.Json.Nodes.JsonNode root, string toolName, string? className, int originalIndex)
        {
            System.Text.Json.Nodes.JsonArray? toolsArray = null;

            if (root is System.Text.Json.Nodes.JsonArray arr)
            {
                toolsArray = arr;
            }
            else if (root is System.Text.Json.Nodes.JsonObject obj)
            {
                if (obj.TryGetPropertyValue("tools", out var toolsNode) || 
                    obj.TryGetPropertyValue("Tools", out toolsNode))
                {
                    toolsArray = toolsNode as System.Text.Json.Nodes.JsonArray;
                }
            }

            if (toolsArray == null) return false;

            return DeleteFromToolsArray(toolsArray, toolName, className, originalIndex);
        }

        private bool DeleteFromComposite(System.Text.Json.Nodes.JsonNode root, string toolName, string? className)
        {
            if (root is not System.Text.Json.Nodes.JsonObject rootObj) return false;

            System.Text.Json.Nodes.JsonArray? modulesArray = null;
            if (rootObj.TryGetPropertyValue("modules", out var modulesNode) ||
                rootObj.TryGetPropertyValue("Modules", out modulesNode))
            {
                modulesArray = modulesNode as System.Text.Json.Nodes.JsonArray;
            }

            if (modulesArray == null) return false;

            // Search through all modules
            foreach (var module in modulesArray)
            {
                if (module is not System.Text.Json.Nodes.JsonObject moduleObj) continue;

                System.Text.Json.Nodes.JsonArray? toolsArray = null;
                if (moduleObj.TryGetPropertyValue("tools", out var toolsNode) ||
                    moduleObj.TryGetPropertyValue("Tools", out toolsNode))
                {
                    toolsArray = toolsNode as System.Text.Json.Nodes.JsonArray;
                }

                if (toolsArray == null) continue;

                if (DeleteFromToolsArray(toolsArray, toolName, className, -1))
                {
                    return true;
                }
            }

            return false;
        }

        private bool DeleteFromToolsArray(System.Text.Json.Nodes.JsonArray toolsArray, string toolName, string? className, int originalIndex)
        {
            // Helper to get property
            string? GetProp(System.Text.Json.Nodes.JsonNode? node, string prop)
            {
                if (node == null) return null;
                if (node[prop] != null) return node[prop]!.GetValue<string>();
                if (node[prop.ToLower()] != null) return node[prop.ToLower()]!.GetValue<string>();
                return null;
            }

            // First try by index if valid
            if (originalIndex >= 0 && originalIndex < toolsArray.Count)
            {
                var node = toolsArray[originalIndex];
                var name = GetProp(node, "name");
                var cls = GetProp(node, "class");
                
                bool nameMatch = name == toolName;
                bool classMatch = string.IsNullOrEmpty(className) || cls == className;
                
                if (nameMatch && classMatch)
                {
                    toolsArray.RemoveAt(originalIndex);
                    return true;
                }
            }
            
            // Fallback: search by name + class (from end to avoid index issues)
            for (int i = toolsArray.Count - 1; i >= 0; i--)
            {
                var node = toolsArray[i];
                var name = GetProp(node, "name");
                var cls = GetProp(node, "class");
                
                bool nameMatch = name == toolName;
                bool classMatch = string.IsNullOrEmpty(className) || cls == className;
                
                if (nameMatch && classMatch)
                {
                    toolsArray.RemoveAt(i);
                    return true;
                }
            }
            
            return false;
        }
    }
}





