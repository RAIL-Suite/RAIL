namespace RailFactory.Core.TransportClients;

/// <summary>
/// Factory for creating transport clients.
/// 
/// In v2.0, only NamedPipe transport is used.
/// External language apps (Python, Node, C++) connect via the Bridge.
/// </summary>
public static class TransportFactory
{
    /// <summary>
    /// Well-known transport type identifiers.
    /// </summary>
    public static class TransportTypes
    {
        /// <summary>
        /// Windows Named Pipes. Used for all local binary communication.
        /// </summary>
        public const string NamedPipe = "namedpipe";
    }
    
    /// <summary>
    /// Creates a transport client for the specified transport type.
    /// </summary>
    /// <param name="transportType">Transport type identifier (defaults to namedpipe)</param>
    /// <returns>New transport client instance</returns>
    public static ITransportClient Create(string transportType = "namedpipe")
    {
        return transportType?.ToLowerInvariant() switch
        {
            TransportTypes.NamedPipe or null or "" => new NamedPipeTransportClient(),
            _ => throw new NotSupportedException(
                $"Unknown transport type: '{transportType}'. Only 'namedpipe' is supported in v2.0.")
        };
    }
    
    /// <summary>
    /// Creates a transport client for a module. Always uses NamedPipe in v2.0.
    /// </summary>
    public static ITransportClient CreateForModule(ModuleManifest module)
    {
        if (module == null)
            throw new ArgumentNullException(nameof(module));
        
        return new NamedPipeTransportClient();
    }
}



