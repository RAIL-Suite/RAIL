using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WpfRagApp.Services.Abstractions
{
    /// <summary>
    /// Common interface for all LLM providers (Gemini, OpenAI, Anthropic, etc.).
    /// </summary>
    public interface ILLMProvider
    {
        /// <summary>
        /// Gets the unique ID of the provider implementation (e.g., "google", "openai").
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Sends a chat request to the LLM.
        /// </summary>
        /// <param name="history">Conversation history.</param>
        /// <param name="toolsJson">Raw JSON of the tools/manifest (to be adapted by the provider).</param>
        /// <param name="config">Configuration overrides.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Standardized response.</returns>
        Task<ProviderResponse> ChatAsync(
            List<ProviderMessage> history, 
            string? toolsJson = null, 
            ProviderConfig? config = null,
            CancellationToken ct = default);

        /// <summary>
        /// Transcribes audio data to text.
        /// </summary>
        Task<string> TranscribeAudioAsync(byte[] audioData, CancellationToken ct = default);
        
        /// <summary>
        /// Returns true if this provider supports audio input for chat (Multimodal).
        /// </summary>
        bool SupportsAudioInput { get; }
    }
}
