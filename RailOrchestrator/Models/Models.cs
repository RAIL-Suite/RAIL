//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace WpfRagApp.Models
//{
//    // Aggiungi queste classi per mappare la richiesta/risposta strutturata

//    public class GeminiTool
//    {
//        // Gemini si aspetta una lista di "function_declarations"
//        [System.Text.Json.Serialization.JsonPropertyName("function_declarations")]
//        public object FunctionDeclarations { get; set; }
//    }

//    public class GeminiFunctionCall
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("name")]
//        public string Name { get; set; }

//        [System.Text.Json.Serialization.JsonPropertyName("args")]
//        public System.Text.Json.Nodes.JsonObject Arguments { get; set; } // Cattura gli argomenti come JSON Node
//    }

//    // Aggiorna GeminiPart per includere FunctionCall
//    public class GeminiPart
//    {
//        public string Text { get; set; }

//        [System.Text.Json.Serialization.JsonPropertyName("functionCall")]
//        public GeminiFunctionCall FunctionCall { get; set; }
//    }

//    // Aggiorna la Request principale per includere Tools
//    public class GeminiGenerateRequestWithTools : GeminiGenerateRequest
//    {
//        [System.Text.Json.Serialization.JsonPropertyName("tools")]
//        public List<GeminiTool> Tools { get; set; }
//    }
//}





