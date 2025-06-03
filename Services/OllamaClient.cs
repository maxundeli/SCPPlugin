using System.Net.Http;
using System.Text;
using Exiled.API.Features;
using Newtonsoft.Json;

namespace MaxunPlugin;

public class OllamaClient
{
    private readonly HttpClient _httpClient;

    public OllamaClient(string baseAddress = "http://192.168.1.182:11411")
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(baseAddress) };
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
    }
    

    
    public async Task<string> SendRequestAsync(string prompt, string model = "gemma3:12b", int num_predict=30)
    {
        try
        {
            // Генерим свой объект
            var requestData = new { prompt, model, num_predict };

            // Сериализация через Newtonsoft
            string jsonRequest = JsonConvert.SerializeObject(requestData);

            var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync("/api/generate", content);
            response.EnsureSuccessStatusCode();

            string? responseContent = await response.Content.ReadAsStringAsync();
            return responseContent;
        }
        catch (Exception e)
        {
            Log.Error($"SendRequestAsync упал с ошибкой: {e}");
            return $"Error: {e}";
        }
    }
}
