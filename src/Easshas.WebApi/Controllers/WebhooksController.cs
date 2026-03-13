using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/webhooks/paystack")]
    public class WebhooksController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IOrderService _orders;
        private readonly IPaystackService _paystack;
        private readonly IEmailSender _email;

        public WebhooksController(IConfiguration configuration, IOrderService orders, IPaystackService paystack, IEmailSender email)
        {
            _configuration = configuration;
            _orders = orders;
            _paystack = paystack;
            _email = email;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Handle()
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            var signature = Request.Headers["x-paystack-signature"].ToString();
            if (!VerifySignature(body, signature))
                return Unauthorized();

            var doc = JsonNode.Parse(body);
            var @event = doc?["event"]?.GetValue<string>();
            var reference = doc?["data"]?["reference"]?.GetValue<string>();
            var customerEmail = doc?["data"]?["customer"]?["email"]?.GetValue<string>() ?? string.Empty;

            if (@event == "charge.success" && !string.IsNullOrWhiteSpace(reference))
            {
                // Extra safety: verify with Paystack before marking paid
                var verified = await _paystack.VerifyTransactionAsync(reference);
                if (verified)
                {
                    // idempotent: MarkOrderPaidAsync handles already-paid gracefully if implemented to check
                    var ok = await _orders.MarkOrderPaidAsync(reference);
                    var order = await _orders.GetOrderByReferenceAsync(reference);
                    if (order != null)
                    {
                        var adminEmail = _configuration["Email:Admin"] ?? "admin@example.com";
                        await _email.SendOrderConfirmationAsync(order, customerEmail, adminEmail);
                    }
                }
            }

            return Ok();
        }

        private bool VerifySignature(string payload, string headerSignature)
        {
            var secret = _configuration["Paystack:SecretKey"] ?? string.Empty; // must be SECRET key
            if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(headerSignature)) return false;

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computed = BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            return string.Equals(computed, headerSignature, StringComparison.Ordinal);
        }
    }
}
