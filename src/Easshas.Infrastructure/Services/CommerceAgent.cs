using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Linq;
using Easshas.Application.Abstractions;
using Easshas.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace Easshas.Infrastructure.Services
{
    public class CommerceAgent : ICommerceAgent
    {
        private readonly IConfiguration _config;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IProductService _products;

        public CommerceAgent(IConfiguration config, IHttpClientFactory httpClientFactory, IProductService products)
        {
            _config = config;
            _httpClientFactory = httpClientFactory;
            _products = products;
        }

        public async Task<AgentIntent> ProcessQueryAsync(string query, string? context = null)
        {
            var apiKey = _config["App:AI:ApiKey"];
            var model = _config["App:AI:Model"] ?? "gpt-4-turbo";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                // Local heuristic fallback when AI key is missing: simple intent and product extraction
                var q = query?.ToLowerInvariant() ?? string.Empty;
                var intent = "unknown";
                if (q.Contains("buy") || q.Contains("purchase") || q.Contains("order") || q.Contains("add to cart")) intent = "purchase";
                else if (q.Contains("track") || q.Contains("status") || q.Contains("where is my")) intent = "track_order";
                else if (q.Contains("price") || q.Contains("how much") || q.Contains("cost")) intent = "price_query";
                else if (q.Contains("recommend") || q.Contains("suggest")) intent = "recommendation";

                string? productName = null;
                try
                {
                    var all = await _products.ListAsync(0, 200);
                    var found = all.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Name) && q.Contains(p.Name.ToLowerInvariant()));
                    if (found != null) productName = found.Name;
                    else
                    {
                        // token match against name/description
                        var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        foreach (var t in tokens.OrderByDescending(s => s.Length))
                        {
                            var f = all.FirstOrDefault(p => (!string.IsNullOrWhiteSpace(p.Name) && p.Name.ToLowerInvariant().Contains(t)) || (!string.IsNullOrWhiteSpace(p.Description) && p.Description.ToLowerInvariant().Contains(t)));
                            if (f != null) { productName = f.Name; break; }
                        }
                    }
                }
                catch
                {
                    // ignore product lookup failures
                }

                return new AgentIntent { Intent = intent, ProductName = productName, RawQuery = query };
            }

            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                        var systemPrompt = @"You are a commerce assistant. Extract the user's intent and entities from their message.
Possible intents: 'purchase', 'track_order', 'price_query', 'recommendation', 'unknown'.
If the user wants to buy or order, use 'purchase'.
If the user provides an email, capture it.
If the user provides an address, capture it.
If the user asks for status, use 'track_order'.
If the user asks for price, use 'price_query'.

Return ONLY a JSON object with this structure:
{
    ""intent"": ""string"",
    ""productName"": ""string or null"",
    ""language"": ""one of: en, pidgin, igbo, yoruba, hausa"",
    ""quantity"": integer,
    ""email"": ""string or null"",
    ""address"": ""string or null"",
    ""entities"": { ""key"": ""value"" }
}";

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = $"Context: {context ?? "none"}\nQuery: {query}" }
                },
                response_format = new { type = "json_object" }
            };

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                return new AgentIntent { Intent = "unknown", RawQuery = query };
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            if (string.IsNullOrWhiteSpace(content)) return new AgentIntent { Intent = "unknown", RawQuery = query };

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var extracted = JsonSerializer.Deserialize<AgentIntentJson>(content, options);

            var result = new AgentIntent
            {
                Intent = extracted?.Intent ?? "unknown",
                ProductName = extracted?.ProductName,
                Language = extracted?.Language,
                Quantity = extracted?.Quantity ?? 1,
                Email = extracted?.Email,
                Address = extracted?.Address,
                RawQuery = query,
                Entities = extracted?.Entities ?? new()
            };

            // If AI returned unknown intent or missing product, attempt a local product lookup to be more helpful
            if ((string.IsNullOrWhiteSpace(result.Intent) || result.Intent == "unknown") || string.IsNullOrWhiteSpace(result.ProductName))
            {
                var q = query?.ToLowerInvariant() ?? string.Empty;
                try
                {
                    var all = await _products.ListAsync(0, 200);
                    var found = all.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Name) && q.Contains(p.Name.ToLowerInvariant()));
                    if (found != null) result.ProductName = result.ProductName ?? found.Name;
                }
                catch
                {
                }
            }

            // Heuristic language detection if AI didn't supply it
            if (string.IsNullOrWhiteSpace(result.Language))
            {
                var ql = query?.ToLowerInvariant() ?? string.Empty;
                if (ql.Contains("ndewo") || ql.Contains("kedu") || ql.Contains("igbo")) result.Language = "igbo";
                else if (ql.Contains("kaabo") || ql.Contains("e nle") || ql.Contains("yoruba")) result.Language = "yoruba";
                else if (ql.Contains("sannu") || ql.Contains("assalamu") || ql.Contains("hausa")) result.Language = "hausa";
                else if (ql.Contains("how far") || ql.Contains("how una") || ql.Contains("pidgin") || ql.Contains("wetin")) result.Language = "pidgin";
                else result.Language = "en";
            }

            return result;
        }

        public async Task<string> GenerateResponseAsync(AgentIntent intent, string? additionalInfo = null)
        {
            var apiKey = _config["App:AI:ApiKey"];
            var model = _config["App:AI:Model"] ?? "gpt-4-turbo";

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return $"I've processed your request for {intent.Intent}. {additionalInfo}";
            }

            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var systemPrompt = "You are a helpful and friendly commerce assistant for Easshas. Generate a natural, polite response based on the detected intent and provided info.";
            var userPrompt = $"Intent: {intent.Intent}\nProduct: {intent.ProductName}\nQuantity: {intent.Quantity}\nAdditional Info: {additionalInfo}\nUser Query: {intent.RawQuery}";

            // If a language was detected/requested, ask the model to respond in that language
            string languageInstruction = string.Empty;
            if (!string.IsNullOrWhiteSpace(intent.Language))
            {
                var code = intent.Language.ToLowerInvariant();
                var langName = code switch
                {
                    "igbo" => "Igbo",
                    "yoruba" => "Yoruba",
                    "hausa" => "Hausa",
                    "pidgin" => "Nigerian Pidgin",
                    "en" => "English",
                    _ => code
                };
                languageInstruction = $"Please respond in {langName}. If you cannot, reply briefly in English and apologize.";
            }

            var messages = new List<object> { new { role = "system", content = systemPrompt + (string.IsNullOrWhiteSpace(languageInstruction) ? string.Empty : "\n" + languageInstruction) }, new { role = "user", content = userPrompt } };

            var requestBody = new
            {
                model = model,
                messages = messages
            };

            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions",
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                return $"Your request for {intent.Intent} was processed, but I had trouble generating a custom message. {additionalInfo}";
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? additionalInfo ?? "Done.";
        }

        private class AgentIntentJson
        {
            [JsonPropertyName("intent")]
            public string? Intent { get; set; }
            [JsonPropertyName("productName")]
            public string? ProductName { get; set; }
            [JsonPropertyName("language")]
            public string? Language { get; set; }
            [JsonPropertyName("quantity")]
            public int? Quantity { get; set; }
            [JsonPropertyName("email")]
            public string? Email { get; set; }
            [JsonPropertyName("address")]
            public string? Address { get; set; }
            [JsonPropertyName("entities")]
            public Dictionary<string, string>? Entities { get; set; }
        }
    }
}

