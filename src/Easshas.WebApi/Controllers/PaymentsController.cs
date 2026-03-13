using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaystackService _paystack;
        private readonly IOrderService _orders;
        private readonly IConfiguration _configuration;

        public PaymentsController(IPaystackService paystack, IOrderService orders, IConfiguration configuration)
        {
            _paystack = paystack;
            _orders = orders;
            _configuration = configuration;
        }

        // Callback endpoint configured in Paystack initialize
        [HttpGet("paystack/callback")]
        
        public async Task<IActionResult> PaystackCallback([FromQuery] string? reference, [FromQuery] string? trxref, [FromQuery] string? redirect)
        {
            var refValue = !string.IsNullOrWhiteSpace(reference) ? reference : trxref;
            if (string.IsNullOrWhiteSpace(refValue))
            {
                return BadRequest(new { message = "Missing reference/trxref" });
            }

            var verified = await _paystack.VerifyTransactionAsync(refValue);
            if (!verified)
            {
                var failUrl = redirect ?? _configuration["Payment:ReturnUrlFailure"];
                if (!string.IsNullOrWhiteSpace(failUrl)) return Redirect(AddQuery(failUrl, ("status", "failed"), ("reference", refValue)));
                return BadRequest(new { message = "Verification failed" });
            }

            // MarkOrderPaidAsync now handles inventory deduction and notifications
            var ok = await _orders.MarkOrderPaidAsync(refValue);
            if (!ok)
            {
                var failUrl = redirect ?? _configuration["Payment:ReturnUrlFailure"];
                if (!string.IsNullOrWhiteSpace(failUrl)) return Redirect(AddQuery(failUrl, ("status", "notfound"), ("reference", refValue)));
                return NotFound(new { message = "Order not found or already processed" });
            }

            // Redirect to success URL if provided or configured
            var successUrl = redirect ?? _configuration["Payment:ReturnUrlSuccess"];
            if (!string.IsNullOrWhiteSpace(successUrl))
            {
                return Redirect(AddQuery(successUrl, ("status", "success"), ("reference", refValue)));
            }
            return Ok(new { message = "Payment verified and order processed", reference = refValue });
        }

        private static string AddQuery(string url, params (string key, string value)[] pairs)
        {
            try
            {
                var uri = new Uri(url);
                var qb = System.Web.HttpUtility.ParseQueryString(uri.Query ?? string.Empty);
                foreach (var (key, value) in pairs)
                {
                    qb[key] = value;
                }
                var builder = new UriBuilder(uri)
                {
                    Query = qb.ToString()
                };
                return builder.Uri.ToString();
            }
            catch
            {
                // Fallback: append query in simple form
                var sep = url.Contains("?") ? "&" : "?";
                var tail = string.Join("&", pairs.Select(p => $"{p.key}={Uri.EscapeDataString(p.value)}"));
                return url + sep + tail;
            }
        }

        public record VerifyRequest(string Reference, string? Redirect);

        [HttpPost("verify")]
      
        public async Task<IActionResult> Verify([FromBody] VerifyRequest req)
        {
            var refValue = req.Reference;
            if (string.IsNullOrWhiteSpace(refValue))
            {
                return BadRequest(new { message = "Missing reference" });
            }

            var verified = await _paystack.VerifyTransactionAsync(refValue);
            if (!verified)
            {
                var failUrl = req.Redirect ?? _configuration["Payment:ReturnUrlFailure"];
                if (!string.IsNullOrWhiteSpace(failUrl)) return Redirect(AddQuery(failUrl, ("status", "failed"), ("reference", refValue)));
                return BadRequest(new { message = "Verification failed" });
            }

            // MarkOrderPaidAsync handles inventory and notifications
            var ok = await _orders.MarkOrderPaidAsync(refValue);

            var successUrl = req.Redirect ?? _configuration["Payment:ReturnUrlSuccess"];
            if (!string.IsNullOrWhiteSpace(successUrl))
            {
                return Redirect(AddQuery(successUrl, ("status", ok ? "success" : "already"), ("reference", refValue)));
            }
            return Ok(new { message = ok ? "Payment verified" : "Already verified", reference = refValue });
        }

        // Fallback: some payment providers redirect to a simple path like /api/payments/{reference}
        // Accept GET and behave like the verify endpoint: verify, mark paid, then redirect to frontend.
        [HttpGet("{reference}")]
        public async Task<IActionResult> VerifyGet([FromRoute] string reference, [FromQuery] string? redirect)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                var failUrl = redirect ?? _configuration["Payment:ReturnUrlFailure"];
                if (!string.IsNullOrWhiteSpace(failUrl)) return Redirect(AddQuery(failUrl, ("status", "failed")));
                return BadRequest(new { message = "Missing reference" });
            }

            var verified = await _paystack.VerifyTransactionAsync(reference);
            if (!verified)
            {
                var failUrl = redirect ?? _configuration["Payment:ReturnUrlFailure"];
                if (!string.IsNullOrWhiteSpace(failUrl)) return Redirect(AddQuery(failUrl, ("status", "failed"), ("reference", reference)));
                return BadRequest(new { message = "Verification failed" });
            }

            var ok = await _orders.MarkOrderPaidAsync(reference);

            var successUrl = redirect ?? _configuration["Payment:ReturnUrlSuccess"];
            if (!string.IsNullOrWhiteSpace(successUrl))
            {
                return Redirect(AddQuery(successUrl, ("status", ok ? "success" : "already"), ("reference", reference)));
            }
            return Ok(new { message = ok ? "Payment verified" : "Already verified", reference });
        }

        // Resilient catch-all for third-party redirects that use different param names or literal placeholders.
        // Example provider might redirect to /api/payments/string or /api/payments/ORD-xxx, or include rrr/transactionId in query.
        [HttpGet("{*any}")]
        public async Task<IActionResult> CatchAllRedirect([FromRoute] string any)
        {
            // Try to find reference-like token in querystring first
            var q = Request.Query;
            string? reference = null;
            string[] keys = new[] { "reference", "trxref", "trxRef", "rrr", "orderId", "transactionId", "transaction_id", "ref" };
            foreach (var k in keys)
            {
                if (q.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v))
                {
                    reference = v.ToString();
                    break;
                }
            }

            // If not in query, use the last path segment (the route captured into `any` may include slashes)
            if (string.IsNullOrWhiteSpace(reference) && !string.IsNullOrWhiteSpace(any))
            {
                var parts = any.Split('/', StringSplitOptions.RemoveEmptyEntries);
                reference = parts.LastOrDefault();
            }

            // If still empty, respond with a helpful message instead of 404 to aid debugging
            if (string.IsNullOrWhiteSpace(reference))
            {
                return BadRequest(new { message = "No payment reference found in path or query. Check provider return URL configuration." });
            }

            // Allow provider to pass a redirect URL via 'redirect' query param
            var redirectUrl = Request.Query.TryGetValue("redirect", out var r) ? r.ToString() : _configuration["Payment:ReturnUrlSuccess"];

            // Verify and mark paid similar to VerifyGet
            var verified = await _paystack.VerifyTransactionAsync(reference);
            if (!verified)
            {
                var failUrl = Request.Query.TryGetValue("redirect", out var rr) ? rr.ToString() : _configuration["Payment:ReturnUrlFailure"];
                if (!string.IsNullOrWhiteSpace(failUrl)) return Redirect(AddQuery(failUrl, ("status", "failed"), ("reference", reference)));
                return BadRequest(new { message = "Verification failed", reference });
            }

            var ok = await _orders.MarkOrderPaidAsync(reference);

            if (!string.IsNullOrWhiteSpace(redirectUrl))
            {
                return Redirect(AddQuery(redirectUrl, ("status", ok ? "success" : "already"), ("reference", reference)));
            }

            return Ok(new { message = ok ? "Payment verified" : "Already verified", reference });
        }
    }
}
