using Newtonsoft.Json;

namespace MaxunPlugin;

public class OllamaChunk
{
    // Поля, которые Ollama может присылать в каждом chunk’е
    [JsonProperty("model")] public string Model { get; set; }

    [JsonProperty("created_at")] public string CreatedAt { get; set; }

    [JsonProperty("response")] public string Response { get; set; }

    [JsonProperty("done")] public bool Done { get; set; }

    [JsonProperty("done_reason")] public string DoneReason { get; set; }

    // Можешь добавить остальные поля: context, total_duration и т.д. если надо парсить
    // [JsonProperty("context")]
    // public int[] Context { get; set; }
    // ...
}
