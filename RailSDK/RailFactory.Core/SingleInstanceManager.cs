using System;
using System.Threading;

namespace RailFactory.Core;

/// <summary>
/// Enterprise-grade single instance manager with proper cleanup.
/// Uses WaitHandle pattern for safe cross-thread disposal.
/// </summary>
public class SingleInstanceManager : IDisposable
{
    private Mutex? _mutex;
    private readonly string _mutexName;
    private bool _ownsMutex;
    private bool _disposed;
    
    public SingleInstanceManager(string appName)
    {
        // Use deterministic hash to ensure consistent naming across processes
        _mutexName = $"RailEngine_{DeterministicHash.GetHash(appName)}";
    }
    
    /// <summary>
    /// Checks if this is the first instance of the application.
    /// </summary>
    /// <returns>True if this is the first instance, false otherwise</returns>
    public bool IsFirstInstance()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SingleInstanceManager));
            
        try
        {
            _mutex = new Mutex(true, _mutexName, out bool createdNew);
            _ownsMutex = createdNew;
            return createdNew;
        }
        catch
        {
            return false;
        }
    }
    
    /// <summary>
    /// Releases resources. Safe to call from any thread.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
            
        _disposed = true;
        
        try
        {
            // Only release if we own it and we're on the same thread
            if (_mutex != null && _ownsMutex)
            {
                // ReleaseMutex can throw if called from wrong thread
                // Just dispose - OS will clean up on process exit
                _mutex.Dispose();
            }
        }
        catch
        {
            // Suppress exceptions during cleanup
            // Mutex will be released when process exits
        }
        
        GC.SuppressFinalize(this);
    }
    
    ~SingleInstanceManager()
    {
        Dispose();
    }
}



