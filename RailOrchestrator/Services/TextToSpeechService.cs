using System.Speech.Synthesis;

namespace WpfRagApp.Services;

/// <summary>
/// Text-to-Speech service for playing LLM responses.
/// Uses Windows built-in speech synthesis.
/// </summary>
public class TextToSpeechService : IDisposable
{
    private readonly SpeechSynthesizer _synth;
    private bool _isSpeaking;
    
    public event Action? OnSpeakingStarted;
    public event Action? OnSpeakingCompleted;
    
    public bool IsSpeaking => _isSpeaking;
    
    public TextToSpeechService()
    {
        _synth = new SpeechSynthesizer();
        _synth.SetOutputToDefaultAudioDevice();
        
        // Use a natural voice if available
        try
        {
            var voices = _synth.GetInstalledVoices()
                .Where(v => v.Enabled)
                .ToList();
            
            // Prefer Italian voice, fallback to any
            var italianVoice = voices.FirstOrDefault(v => 
                v.VoiceInfo.Culture.Name.StartsWith("it", StringComparison.OrdinalIgnoreCase));
            
            if (italianVoice != null)
                _synth.SelectVoice(italianVoice.VoiceInfo.Name);
        }
        catch { /* Use default voice */ }
        
        _synth.SpeakCompleted += (s, e) =>
        {
            _isSpeaking = false;
            OnSpeakingCompleted?.Invoke();
        };
    }
    
    /// <summary>
    /// Speak the given text asynchronously.
    /// </summary>
    public void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        
        Stop(); // Stop any previous speech
        
        _isSpeaking = true;
        OnSpeakingStarted?.Invoke();
        _synth.SpeakAsync(text);
    }
    
    /// <summary>
    /// Stop speaking.
    /// </summary>
    public void Stop()
    {
        if (_isSpeaking)
        {
            _synth.SpeakAsyncCancelAll();
            _isSpeaking = false;
        }
    }
    
    /// <summary>
    /// Set speaking rate (-10 to 10, 0 is normal).
    /// </summary>
    public void SetRate(int rate)
    {
        _synth.Rate = Math.Clamp(rate, -10, 10);
    }
    
    public void Dispose()
    {
        Stop();
        _synth.Dispose();
    }
}





