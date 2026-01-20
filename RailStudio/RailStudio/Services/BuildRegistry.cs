using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RailStudio.Services
{
    public interface IBuildRegistry
    {
        bool CanBuild(string filePath);
        string GetRuntimeType(string filePath);
        IEnumerable<string> GetSupportedExtensions();
    }

    public class BuildRegistry : IBuildRegistry
    {
        private readonly Dictionary<string, string> _extensionMap;

        public BuildRegistry()
        {
            _extensionMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Scripting / Dynamic
                { ".py", "python" },
                { ".js", "node" },
                { ".ts", "typescript" },
                
                // Compiled / Static
                { ".cs", "csharp" },
                { ".java", "java" },
                { ".go", "go" },
                { ".cpp", "cpp" },
                { ".c", "c" },
                { ".rs", "rust" },
                
                // Binary / Artifacts (Import mode)
                { ".exe", "binary" },
                { ".dll", "binary" },
                { ".jar", "binary_java" }
            };
        }

        public bool CanBuild(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            var ext = Path.GetExtension(filePath);
            return !string.IsNullOrEmpty(ext) && _extensionMap.ContainsKey(ext);
        }

        public string GetRuntimeType(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return "unknown";
            var ext = Path.GetExtension(filePath);
            return _extensionMap.TryGetValue(ext, out var type) ? type : "unknown";
        }

        public IEnumerable<string> GetSupportedExtensions()
        {
            return _extensionMap.Keys;
        }
    }
}




