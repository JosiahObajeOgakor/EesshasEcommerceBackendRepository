using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Application.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Easshas.Infrastructure.Services
{
    public class OpenAiPlanner : IPlanner
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly ILogger<OpenAiPlanner> _logger;

        public OpenAiPlanner(IHttpClientFactory httpFactory, IConfiguration config, ILogger<OpenAiPlanner> logger)
        {
            _http = httpFactory.CreateClient("OpenAI");
            _apiKey = config["OpenAI:ApiKey"] ?? config["App:AI:ApiKey"] ?? string.Empty;
            _logger = logger;
        }

        public async Task<List<AgentAction>> PlanAsync(string userId, string text, string? language = "en")
        {
            try
            {
                var system = "You are an assistant that outputs a JSON array of actions with 'Type' and 'Params'. Respond ONLY with a JSON array.";
                var userPrompt = $"User: {text}\n\nReturn a JSON array of actions where each action is {{ \"Type\": string, \"Params\": {{...}} }}";

                var payload = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "system", content = system },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.2,
                    max_tokens = 512
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

                // Extract first JSON array in content
                var start = content.IndexOf('[');
                var end = content.LastIndexOf(']');
                if (start >= 0 && end > start)
                {
                    var jsonArr = content.Substring(start, end - start + 1);
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    try
                    {
                        var actions = JsonSerializer.Deserialize<List<AgentAction>>(jsonArr, opts);
                        return actions ?? new List<AgentAction>();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed parse actions JSON");
                        return new List<AgentAction>();
                    }
                }

                return new List<AgentAction>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAiPlanner failed");
                return new List<AgentAction>();
            }
        }
    }
}
