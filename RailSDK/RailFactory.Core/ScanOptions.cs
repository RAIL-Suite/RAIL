namespace RailFactory.Core
{
    /// <summary>
    /// Scan options for binary method extraction.
    /// Defaults match current behavior (public methods only).
    /// </summary>
    public class ScanOptions
    {
        // Visibility
        public bool IncludePublic { get; set; } = true;
        public bool IncludePrivate { get; set; } = false;
        public bool IncludeProtected { get; set; } = false;
        public bool IncludeInternal { get; set; } = false;
        
        // Method Types
        public bool IncludeStatic { get; set; } = true;
        public bool IncludeInstance { get; set; } = true;
        public bool IncludeConstructors { get; set; } = false;
        public bool IncludeProperties { get; set; } = false;
        public bool IncludeOperators { get; set; } = false;
        
        // Filtering
        public bool ExcludeSystemObjectMethods { get; set; } = true;
        public bool ExcludeCompilerGenerated { get; set; } = true;
        public bool RemoveDuplicates { get; set; } = true;
        
        // Schema
        public bool ExpandComplexTypes { get; set; } = true;
        public int MaxReflectionDepth { get; set; } = 3;
        
        // Naming
        public bool UseClassPrefix { get; set; } = true;
    }
}



