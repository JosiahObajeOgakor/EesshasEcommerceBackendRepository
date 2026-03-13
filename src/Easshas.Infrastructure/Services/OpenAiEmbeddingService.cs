using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Easshas.Infrastructure.Services
{
    public class OpenAiEmbeddingService : IEmbeddingService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly ILogger<OpenAiEmbeddingService> _logger;

        public OpenAiEmbeddingService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<OpenAiEmbeddingService> logger)
        {
            _http = httpFactory.CreateClient("OpenAI");
            _apiKey = config["OpenAI:ApiKey"] ?? config["App:AI:ApiKey"] ?? string.Empty;
            _logger = logger;
        }

        public async Task<float[]> GetEmbeddingAsync(string text)
        {
            try
            {
                var payload = new { input = text, model = "text-embedding-3-small" };
                var json = JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/embeddings");
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                req.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var resp = await _http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                var s = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(s);
                var arr = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
                var floats = new float[arr.GetArrayLength()];
                for (int i = 0; i < floats.Length; i++) floats[i] = arr[i].GetSingle();
                return floats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Embedding request failed");
                return Array.Empty<float>();
            }
        }
    }
}
