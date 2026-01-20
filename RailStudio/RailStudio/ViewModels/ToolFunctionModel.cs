using RailStudio.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Collections.ObjectModel;

namespace RailStudio.ViewModels
{
    /// <summary>
    /// View model wrapper for RailTool with duplicate detection and UI binding support.
    /// </summary>
    public class ToolFunctionModel
    {
        private readonly RailTool _tool;

        public ToolFunctionModel(RailTool tool)
        {
            _tool = tool ?? throw new ArgumentNullException(nameof(tool));
        }

        /// <summary>
        /// The underlying RailTool data model.
        /// </summary>
        public RailTool Tool => _tool;

        /// <summary>
        /// Original index in the manifest tools list.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Function name.
        /// </summary>
        public string Name => _tool.Name;

        /// <summary>
        /// Function description.
        /// </summary>
        public string Description => _tool.Description;

        /// <summary>
        /// Formatted parameter signature for display.
        /// </summary>
        public string ParametersFormatted => _tool.ParametersFormatted;

        /// <summary>
        /// List of parsed parameters for dynamic column binding.
        /// </summary>
        public ObservableCollection<ParameterInfo> ParsedParameters => _tool.ParsedParameters;

        /// <summary>
        /// Indicates if this function is a duplicate (same name + signature).
        /// </summary>
        public bool IsDuplicate { get; set; }

        /// <summary>
        /// Indicates if this function differs only by signature (Overload).
        /// </summary>
        public bool IsOverload { get; set; }

        /// <summary>
        /// Source assembly name (e.g., "AgentTest.dll"). Populated for composite manifests.
        /// </summary>
        public string Assembly { get; set; } = string.Empty;

        /// <summary>
        /// Assembly classification ("Module" or "Dependency"). Populated for composite manifests.
        /// </summary>
        public string Type { get; set; } = string.Empty;
        
        /// <summary>
        /// Transport protocol for IPC communication (e.g., "namedpipe", "stdin", "http").
        /// Populated for composite manifests.
        /// </summary>
        public string Transport { get; set; } = string.Empty;

        /// <summary>
        /// Full qualified class name containing the method (e.g., "Namespace.ClassName").
        /// Reads directly from the deserialized manifest tool.
        /// </summary>
        public string ClassName => _tool.ClassName ?? string.Empty;

        /// <summary>
        /// Computes a deterministic signature hash for duplicate detection.
        /// Signature = Name + Parameter Types (order-sensitive).
        /// </summary>
        public string GetSignatureHash()
        {
            var signatureBuilder = new StringBuilder();
            
            // Add function name
            signatureBuilder.Append(_tool.Name);
            signatureBuilder.Append('|');

            // Add parameter types in order
            var parameters = _tool.ParsedParameters;
            if (parameters != null && parameters.Count > 0)
            {
                foreach (var param in parameters.OrderBy(p => p.Name)) // Sort for consistency
                {
                    signatureBuilder.Append(param.Name);
                    signatureBuilder.Append(':');
                    signatureBuilder.Append(param.JsonType);
                    signatureBuilder.Append(',');
                }
            }

            var signature = signatureBuilder.ToString();

            // Compute SHA256 hash for efficient comparison
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(signature));
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Gets a human-readable summary for confirmation dialogs.
        /// </summary>
        public string GetDisplaySummary()
        {
            var summary = new StringBuilder();
            summary.AppendLine($"Function: {Name}");
            summary.AppendLine($"Description: {Description}");
            summary.AppendLine($"Parameters: {ParametersFormatted}");
            return summary.ToString();
        }
    }
}




