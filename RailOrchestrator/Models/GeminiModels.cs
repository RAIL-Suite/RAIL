//using System.Collections.Generic;
//using System.Text.Json.Serialization;

//namespace WpfRagApp.Models
//{
//    public class GeminiBatchEmbedRequest
//    {
//        [JsonPropertyName("requests")]
//        public List<GeminiEmbedRequest> Requests { get; set; }
//    }

//    public class GeminiBatchEmbedResponse
//    {
//        [JsonPropertyName("embeddings")]
//        public List<GeminiEmbedding> Embeddings { get; set; }
//    }

//    public class GeminiEmbedRequest
//    {
//        [JsonPropertyName("model")]
//        public string Model { get; set; } = "models/embedding-001";

//        [JsonPropertyName("content")]
//        public GeminiContent Content { get; set; }
//    }

//    public class GeminiContent
//    {
//        [JsonPropertyName("parts")]
//        public List<GeminiPart> Parts { get; set; }
//    }

//    public class GeminiEmbedResponse
//    {
//        [JsonPropertyName("embedding")]
//        public GeminiEmbedding Embedding { get; set; }
//    }

//    public class GeminiEmbedding
//    {
//        [JsonPropertyName("values")]
//        public List<float> Values { get; set; }
//    }

//    public class GeminiGenerateRequest
//    {
//        [JsonPropertyName("contents")]
//        public List<GeminiContent> Contents { get; set; }
//    }

//    public class GeminiGenerateResponse
//    {
//        [JsonPropertyName("candidates")]
//        public List<GeminiCandidate> Candidates { get; set; }
//    }

//    public class GeminiCandidate
//    {
//        [JsonPropertyName("content")]
//        public GeminiContent Content { get; set; }
//    }
//}


using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace WpfRagApp.Models
{
    // --- RICHIESTE E RISPOSTE GENERATIVE (CHAT & TOOLS) ---

    public class GeminiGenerateRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; }

        [JsonPropertyName("tools")]
        public List<GeminiTool> Tools { get; set; }

        [JsonPropertyName("tool_config")]
        public GeminiToolConfig ToolConfig { get; set; }
    }

    public class GeminiGenerateResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate> Candidates { get; set; }
    }

    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string FinishReason { get; set; }
    }

    // --- STRUTTURA CONTENUTO (MESSAGGI) ---

    public class GeminiContent
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } // "user", "model", o "function"

        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; }
    }

    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("functionCall")]
        public GeminiFunctionCall FunctionCall { get; set; }

        [JsonPropertyName("functionResponse")]
        public GeminiFunctionResponse FunctionResponse { get; set; }
    }

    // --- FUNCTION CALLING & TOOLS ---

    public class GeminiTool
    {
        [JsonPropertyName("function_declarations")]
        public object FunctionDeclarations { get; set; } // Passiamo il nodo JSON diretto
    }

    public class GeminiToolConfig
    {
        [JsonPropertyName("function_calling_config")]
        public GeminiFunctionCallingConfig FunctionCallingConfig { get; set; }
    }

    public class GeminiFunctionCallingConfig
    {
        [JsonPropertyName("mode")]
        public string Mode { get; set; } // "AUTO", "ANY", "NONE"
    }

    public class GeminiFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("args")]
        public JsonObject Arguments { get; set; } // Argomenti dinamici
    }

    public class GeminiFunctionResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("response")]
        public object Response { get; set; }
    }

    // --- EMBEDDINGS (Se li usi ancora) ---

    public class GeminiBatchEmbedRequest
    {
        [JsonPropertyName("requests")]
        public List<GeminiEmbedRequest> Requests { get; set; }
    }

    public class GeminiBatchEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<GeminiEmbedding> Embeddings { get; set; }
    }

    public class GeminiEmbedRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "models/embedding-001";

        [JsonPropertyName("content")]
        public GeminiContent Content { get; set; }
    }

    public class GeminiEmbedResponse
    {
        [JsonPropertyName("embedding")]
        public GeminiEmbedding Embedding { get; set; }
    }

    public class GeminiEmbedding
    {
        [JsonPropertyName("values")]
        public List<float> Values { get; set; }
    }
}




