using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/payments/webhook")]
    public class PaystackWebhookController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IOrderService _orders;
        private readonly IPaystackService _paystack;
        private readonly ILogger<PaystackWebhookController> _logger;

        public PaystackWebhookController(IConfiguration config, IOrderService orders, IPaystackService paystack, ILogger<PaystackWebhookController> logger)
        {
            _config = config;
            _orders = orders;
            _paystack = paystack;
            _logger = logger;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Receive()
        {
            // Read raw body
            using var sr = new StreamReader(Request.Body);
            var body = await sr.ReadToEndAsync();

            // Verify signature
            var header = Request.Headers["X-Paystack-Signature"].ToString();
            var secret = _config["Paystack:SecretKey"] ?? _config["Paystack__SecretKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogWarning("Paystack webhook received but no secret configured");
                return BadRequest();
            }

            try
            {
                var secretBytes = Encoding.UTF8.GetBytes(secret);
                using var hmac = new HMACSHA512(secretBytes);
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                var computed = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                if (string.IsNullOrWhiteSpace(header) || !string.Equals(header.Trim(), computed, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Paystack webhook signature mismatch. Received: {Header} Computed: {Computed}", header, computed);
                    return BadRequest();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying Paystack webhook signature");
                return BadRequest();
            }

            // Parse body and extract reference
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                // Depending on event, data.reference may be present
                if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    string? reference = null;
                    if (data.TryGetProperty("reference", out var r)) reference = r.GetString();
                    else if (data.TryGetProperty("transaction", out var tx) && tx.TryGetProperty("reference", out var r2)) reference = r2.GetString();

                    if (!string.IsNullOrWhiteSpace(reference))
                    {
                        // Optionally verify via Paystack API for extra safety
                        try
                        {
                            var verified = await _paystack.VerifyTransactionAsync(reference);
                            if (!verified)
                            {
                                _logger.LogWarning("Paystack webhook verification API failed for reference {Ref}", reference);
                                return BadRequest();
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error calling Paystack verify for {Ref}, proceeding to mark paid optimistically", reference);
                        }

                        // Mark order paid
                        try
                        {
                            var ok = await _orders.MarkOrderPaidAsync(reference);
                            if (ok) return Ok();
                            _logger.LogWarning("MarkOrderPaidAsync returned false for reference {Ref}", reference);
                            return NotFound();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error marking order paid for reference {Ref}", reference);
                            return StatusCode(500);
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON in Paystack webhook payload");
                return BadRequest();
            }

            return BadRequest();
        }
    }
}
