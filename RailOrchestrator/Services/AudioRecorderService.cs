using NAudio.Wave;
using System.IO;

namespace WpfRagApp.Services;

/// <summary>
/// Thread-safe audio recorder service.
/// Records audio from the default microphone with proper synchronization.
/// </summary>
/// <remarks>
/// Uses a state machine pattern (Idle → Recording → Stopping → Idle) 
/// with lock-based synchronization to prevent race conditions.
/// </remarks>
public class AudioRecorderService : IDisposable
{
    #region State Machine
    
    private enum RecorderState
    {
        Idle,
        Recording,
        Stopping
    }
    
    private readonly object _stateLock = new();
    private RecorderState _state = RecorderState.Idle;
    private bool _isDisposed;
    
    #endregion
    
    #region Audio Components
    
    private WaveInEvent? _waveIn;
    private MemoryStream? _memoryStream;
    private WaveFileWriter? _writer;
    
    // Thread-safe reference for callback
    private volatile WaveFileWriter? _activeWriter;
    
    #endregion
    
    #region Events
    
    public event Action? OnRecordingStarted;
    public event Action? OnRecordingStopped;
    public event Action<float>? OnAudioLevelChanged;
    
    #endregion
    
    #region Properties
    
    public bool IsRecording
    {
        get
        {
            lock (_stateLock)
            {
                return _state == RecorderState.Recording;
            }
        }
    }
    
    #endregion
    
    #region Public API
    
    /// <summary>
    /// Start recording from default microphone.
    /// Thread-safe: can be called from any thread.
    /// </summary>
    public void StartRecording()
    {
        lock (_stateLock)
        {
            ThrowIfDisposed();
            
            if (_state != RecorderState.Idle)
            {
                return; // Already recording or stopping
            }
            
            InitializeRecording();
            _state = RecorderState.Recording;
        }
        
        // Start recording outside lock to prevent deadlocks
        _waveIn!.StartRecording();
        OnRecordingStarted?.Invoke();
    }
    
    /// <summary>
    /// Stop recording and return audio bytes (WAV format).
    /// Thread-safe: can be called from any thread.
    /// </summary>
    public byte[] StopRecording()
    {
        WaveInEvent? waveIn;
        WaveFileWriter? writer;
        MemoryStream? memoryStream;
        
        lock (_stateLock)
        {
            if (_state != RecorderState.Recording)
            {
                return Array.Empty<byte>();
            }
            
            _state = RecorderState.Stopping;
            
            // Capture references for cleanup
            waveIn = _waveIn;
            writer = _writer;
            memoryStream = _memoryStream;
            
            // Clear active writer FIRST to stop callbacks from writing
            _activeWriter = null;
        }
        
        // Stop recording outside lock
        waveIn?.StopRecording();
        
        // Wait for any in-flight callbacks to complete
        Thread.Sleep(50);
        
        // Now safe to flush and get data
        byte[] audioData;
        try
        {
            writer?.Flush();
            audioData = memoryStream?.ToArray() ?? Array.Empty<byte>();
        }
        catch
        {
            audioData = Array.Empty<byte>();
        }
        
        // Cleanup resources
        CleanupResources(waveIn, writer, memoryStream);
        
        lock (_stateLock)
        {
            _waveIn = null;
            _writer = null;
            _memoryStream = null;
            _state = RecorderState.Idle;
        }
        
        OnRecordingStopped?.Invoke();
        return audioData;
    }
    
    #endregion
    
    #region Private Methods
    
    private void InitializeRecording()
    {
        _memoryStream = new MemoryStream();
        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1) // 16kHz, 16-bit, mono (Gemini compatible)
        };
        
        _writer = new WaveFileWriter(new IgnoreDisposeStream(_memoryStream), _waveIn.WaveFormat);
        _activeWriter = _writer; // Set active reference for callbacks
        
        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStoppedInternal;
    }
    
    /// <summary>
    /// Thread-safe callback for audio data.
    /// Uses local capture to prevent null reference.
    /// </summary>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        // Capture reference locally - this is the key to thread safety
        var writer = _activeWriter;
        if (writer == null) return;
        
        try
        {
            writer.Write(e.Buffer, 0, e.BytesRecorded);
            
            // Calculate audio level for visual feedback
            float max = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                if (i + 1 >= e.BytesRecorded) break;
                var sample = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
                var abs = Math.Abs(sample / 32768f);
                if (abs > max) max = abs;
            }
            OnAudioLevelChanged?.Invoke(max);
        }
        catch
        {
            // Ignore write errors during shutdown
        }
    }
    
    private void OnRecordingStoppedInternal(object? sender, StoppedEventArgs e)
    {
        var writer = _activeWriter;
        try
        {
            writer?.Flush();
        }
        catch
        {
            // Ignore flush errors
        }
    }
    
    private static void CleanupResources(WaveInEvent? waveIn, WaveFileWriter? writer, MemoryStream? memoryStream)
    {
        try { writer?.Dispose(); } catch { }
        try { waveIn?.Dispose(); } catch { }
        try { memoryStream?.Dispose(); } catch { }
    }
    
    private void ThrowIfDisposed()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(AudioRecorderService));
        }
    }
    
    #endregion
    
    #region IDisposable
    
    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
        }
        
        if (_state == RecorderState.Recording)
        {
            StopRecording();
        }
        
        CleanupResources(_waveIn, _writer, _memoryStream);
        
        GC.SuppressFinalize(this);
    }
    
    #endregion
}

#region Helper Classes

/// <summary>
/// Wraps a stream to prevent disposal (needed for NAudio WaveFileWriter).
/// </summary>
internal class IgnoreDisposeStream : Stream
{
    private readonly Stream _inner;
    
    public IgnoreDisposeStream(Stream inner) => _inner = inner;
    
    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position { get => _inner.Position; set => _inner.Position = value; }
    
    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    
    protected override void Dispose(bool disposing) { /* Don't dispose inner */ }
}

#endregion





