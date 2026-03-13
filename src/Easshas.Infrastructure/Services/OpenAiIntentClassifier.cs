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
    public class OpenAiIntentClassifier : IIntentClassifier
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly ILogger<OpenAiIntentClassifier> _logger;

        public OpenAiIntentClassifier(IHttpClientFactory httpFactory, IConfiguration config, ILogger<OpenAiIntentClassifier> logger)
        {
            _http = httpFactory.CreateClient("OpenAI");
            _apiKey = config["OpenAI:ApiKey"] ?? config["App:AI:ApiKey"] ?? string.Empty;
            _logger = logger;
        }

        public async Task<(string Label, double Confidence)> ClassifyAsync(string message)
        {
            try
            {
                // Few-shot prompt: ask model to return strict JSON
                var system = "You are a classification assistant. Respond ONLY with a single JSON object with keys 'label' and 'confidence'. 'label' must be either 'agentic' or 'chat'. 'confidence' must be a number between 0 and 1.";
                var examples = new[]
                {
                    new { user = "I want to buy the red dress and checkout now.", assistant = "{ \"label\": \"agentic\", \"confidence\": 0.99 }" },
                    new { user = "How do I return an item?", assistant = "{ \"label\": \"chat\", \"confidence\": 0.95 }" },
                    new { user = "What's your shipping time?", assistant = "{ \"label\": \"chat\", \"confidence\": 0.90 }" }
                };

                var userPrompt = new StringBuilder();
                userPrompt.AppendLine("Classify the following message into 'agentic' (user intends to perform purchases/actions) or 'chat' (informational/conversational). Respond only with JSON.");
                userPrompt.AppendLine();
                foreach (var ex in examples)
                {
                    userPrompt.AppendLine($"User: {ex.user}");
                    userPrompt.AppendLine($"Assistant: {ex.assistant}");
                    userPrompt.AppendLine();
                }
                userPrompt.AppendLine($"User: {message}");
                userPrompt.AppendLine("Assistant:");

                var payload = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "system", content = system },
                        new { role = "user", content = userPrompt.ToString() }
                    },
                    temperature = 0.0,
                    max_tokens = 60
                };

                var json = JsonSerializer.Serialize(payload);
                using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions");
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                req.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var resp = await _http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                var s = await resp.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(s);
                var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;

                // Extract JSON object
                var start = content.IndexOf('{');
                var end = content.LastIndexOf('}');
                if (start >= 0 && end > start)
                {
                    var jsonObj = content.Substring(start, end - start + 1);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    try
                    {
                        var parsed = JsonSerializer.Deserialize<ClassifierResult>(jsonObj, opts);
                        if (parsed != null)
                        {
                            var label = (parsed.Label ?? "chat").Trim().ToLowerInvariant();
                            var conf = Math.Max(0.0, Math.Min(1.0, parsed.Confidence));
                            return (label, conf);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed parsing classifier JSON: {json}", jsonObj);
                    }
                }

                return ("chat", 0.0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAiIntentClassifier failed");
                return ("chat", 0.0);
            }
        }

        private class ClassifierResult
        {
            public string? Label { get; set; }
            public double Confidence { get; set; }
        }
    }
}
