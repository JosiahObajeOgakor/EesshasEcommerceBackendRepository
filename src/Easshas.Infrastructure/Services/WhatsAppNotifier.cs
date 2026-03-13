using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace Easshas.Infrastructure.Services
{
    public class WhatsAppNotifier : IWhatsAppNotifier
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public WhatsAppNotifier(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public Task NotifyOrderConfirmedAsync(Order order, string userPhone, string adminPhone)
        {
            var provider = _configuration["WhatsApp:Provider"] ?? "Meta";
            if (provider.Equals("Meta", System.StringComparison.OrdinalIgnoreCase))
            {
                return SendViaMetaAsync(order, userPhone, adminPhone);
            }
            return Task.CompletedTask;
        }

        public async Task SendTextAsync(string toPhone, string text)
        {
            var token = _configuration["WhatsApp:AccessToken"];
            var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(phoneNumberId)) return;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var payload = JsonSerializer.Serialize(new
            {
                messaging_product = "whatsapp",
                to = toPhone,
                type = "text",
                text = new { body = text }
            });
            var url = $"https://graph.facebook.com/v20.0/{phoneNumberId}/messages";
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await client.PostAsync(url, content);
        }

        private async Task SendViaMetaAsync(Order order, string userPhone, string adminPhone)
        {
            var token = _configuration["WhatsApp:AccessToken"];
            var phoneNumberId = _configuration["WhatsApp:PhoneNumberId"];
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(phoneNumberId)) return;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var message = $"Order {order.Id} confirmed. Total: {order.TotalAmount} {order.Currency}.";
            var payload = JsonSerializer.Serialize(new
            {
                messaging_product = "whatsapp",
                to = userPhone,
                type = "text",
                text = new { body = message }
            });

            var url = $"https://graph.facebook.com/v20.0/{phoneNumberId}/messages";
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await client.PostAsync(url, content);

            if (!string.IsNullOrWhiteSpace(adminPhone))
            {
                var payloadAdmin = JsonSerializer.Serialize(new
                {
                    messaging_product = "whatsapp",
                    to = adminPhone,
                    type = "text",
                    text = new { body = $"Buyer order {order.Id} confirmed. Total: {order.TotalAmount} {order.Currency}." }
                });
                await client.PostAsync(url, new StringContent(payloadAdmin, Encoding.UTF8, "application/json"));
            }
        }
    }
}
