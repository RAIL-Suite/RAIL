namespace RailFactory.Core;

/// <summary>
/// Detects the runtime type of a binary file.
/// Implementations analyze file headers, metadata, and structure to determine
/// if a file is a .NET assembly, Java JAR, Python package, etc.
/// </summary>
public interface IRuntimeDetector
{
    /// <summary>
    /// Analyzes a binary file and returns its runtime type.
    /// </summary>
    /// <param name="filePath">Absolute path to the binary file</param>
    /// <returns>The detected runtime type, or RuntimeType.Unknown if not recognized</returns>
    RuntimeType Detect(string filePath);
    
    /// <summary>
    /// Quick check if this detector can handle the given file without full detection.
    /// Used for optimization - only run full Detect() if CanHandle returns true.
    /// </summary>
    /// <param name="filePath">Absolute path to the binary file</param>
    /// <returns>True if this detector should attempt to detect this file</returns>
    bool CanHandle(string filePath);
}

/// <summary>
/// Scans a binary file to extract callable methods/functions.
/// Uses reflection, bytecode analysis, or other introspection techniques
/// depending on the runtime type.
/// </summary>
public interface IRuntimeScanner
{
    /// <summary>
    /// Scans a binary and returns all publicly accessible methods.
    /// </summary>
    /// <param name="filePath">Absolute path to the binary file</param>
    /// <param name="options">Optional scan configuration options</param>
    /// <returns>List of methods with their signatures and metadata</returns>
    List<RailMethod> ScanBinary(string filePath, RailFactory.Core.ScanOptions? options = null);
}

/// <summary>
/// Executes methods on a running application instance.
/// Handles inter-process communication and method invocation via the
    /// appropriate mechanism (Reflection for .NET, JNI for Java, etc.)
/// </summary>
public interface IRuntimeExecutor
{
    /// <summary>
    /// Initializes the executor with a reference to the running application instance.
    /// Called once when the application starts (e.g., in RailEngine.Ignite())
    /// </summary>
    /// <param name="appInstance">The application instance (e.g., WPF App or MainWindow)</param>
    void Initialize(object appInstance);
    
    /// <summary>
    /// Executes a method on the initialized application instance.
    /// </summary>
    /// <param name="command">The command containing method name and arguments</param>
    /// <returns>The result of the method invocation</returns>
    object Execute(RailCommand command);
}

/// <summary>
/// Manages inter-process communication between the LLM client and running application.
/// Handles single-instance enforcement, command routing, and result serialization.
/// </summary>
public interface IRuntimeIpcProvider
{
    /// <summary>
    /// Starts an IPC server to listen for commands from external clients.
    /// Runs on a background thread to avoid blocking the UI.
    /// </summary>
    /// <param name="appInstance">The application instance to execute commands against</param>
    void StartServer(object appInstance);
    
    /// <summary>
    /// Sends a command to a running application instance via IPC.
    /// Used when a second instance is launched - it sends the command to the first instance.
    /// </summary>
    /// <param name="pipeName">Name of the IPC channel (pipe, socket, etc.)</param>
    /// <param name="command">The command to execute</param>
    /// <returns>JSON result from the command execution</returns>
    string SendCommand(string pipeName, RailCommand command);
}



