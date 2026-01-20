namespace RailFactory.Core;

/// <summary>
/// Defines the type of runtime/binary that Rail Factory can control.
/// This enum is extensible - new runtime types can be added without modifying existing code.
/// </summary>
public enum RuntimeType
{
    /// <summary>
    /// Unknown or unsupported binary format
    /// </summary>
    Unknown,
    
    /// <summary>
    /// Source code scripts (.py, .cs files) - MODE 1 (existing)
    /// Compiled on-the-fly and executed via runtime.py
    /// </summary>
    Script,
    
    /// <summary>
    /// .NET compiled binary (.exe or .dll) - MODE 2 (v1.0)
    /// Executed via IPC + Reflection
    /// </summary>
    DotNetBinary,
    
    /// <summary>
    /// Java compiled archive (.jar) - Future v1.1
    /// Will use JNI bridge or process communication
    /// </summary>
    JavaBinary,
    
    /// <summary>
    /// Python packaged application (.pyz, .pex) - Future v1.2
    /// Self-contained Python with embedded interpreter
    /// </summary>
    PythonBinary,
    
    /// <summary>
    /// Node.js packaged executable - Future v1.3
    /// Created with pkg or nexe
    /// </summary>
    NodeBinary,
    
    /// <summary>
    /// Go compiled binary - Future v1.4
    /// Native executable with cgo support
    /// </summary>
    GoBinary,
    
    /// <summary>
    /// C++ project with headers (.h/.hpp) - v2.0 Polyglot
    /// Scanned via Clang AST parsing for exported functions
    /// </summary>
    CppBinary,
    
    /// <summary>
    /// Running web service (HTTP/gRPC) - MODE 3 (future)
    /// Controlled via REST API or gRPC calls
    /// </summary>
    WebService,

    /// <summary>
    /// Generative PowerShell Automation - v3.0
    /// Agent writes and executes text scripts to control OS/COM
    /// </summary>
    GenerativePowerShell
}



