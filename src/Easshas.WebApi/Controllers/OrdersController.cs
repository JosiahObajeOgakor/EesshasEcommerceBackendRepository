using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Easshas.WebApi.Dtos;

namespace Easshas.WebApi.Controllers
{
    [ApiController]
    [Route("api/orders")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "User,Admin")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orders;
        private readonly IPaystackService _paystack;
        private readonly IEmailValidationService _emailValidation;
        private readonly Easshas.Application.Abstractions.INotificationService _notifications;

        public OrdersController(IOrderService orders, IPaystackService paystack, IEmailValidationService emailValidation, Easshas.Application.Abstractions.INotificationService notifications)
        {
            _orders = orders;
            _paystack = paystack;
            _emailValidation = emailValidation;
            _notifications = notifications;
        }

        public record CreateOrderRequest(Guid ProductId, int Quantity, string FullName, string Line1, string? Line2, string City, string State, string Country, string PostalCode, string PhoneNumber, DateTime? ExpectedDeliveryDate, string EmailForPayment, string CallbackUrl);

        public record CreateOrderFromCartRequest(Easshas.Application.Abstractions.CartDto Cart, string FullName, string Line1, string? Line2, string City, string State, string Country, string PostalCode, string PhoneNumber, DateTime? ExpectedDeliveryDate, string EmailForPayment, string CallbackUrl);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest req)
        {
            try
            {
                var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
                if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var userId))
                {
                    return Unauthorized(new { message = "User ID is required for checkout." });
                }

                var address = new Address(req.FullName, req.Line1, req.Line2, req.City, req.State, req.Country, req.PostalCode, req.PhoneNumber);
                var email = (req.EmailForPayment ?? string.Empty).Trim();
                if (!_emailValidation.IsAcceptable(email))
                {
                    return BadRequest(new { message = "Invalid or unrecognized email provider.", recognizedProviders = _emailValidation.GetRecognizedDomains() });
                }
                var order = await _orders.CreateOrderAsync(userId, req.ProductId, req.Quantity, address, req.ExpectedDeliveryDate);

                var reference = $"ORD-{order.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                var init = await _paystack.InitializeTransactionAsync(order.TotalAmount, email, reference, req.CallbackUrl);
                await _orders.SetPaystackReferenceAsync(order.Id, init.Reference);

                // Build public tracking URL (uses current host)
                var trackingUrl = $"{Request.Scheme}://{Request.Host}/api/orders/status?reference={init.Reference}";

                    // Notify user and admins about new order and provide tracking link in email
                    try
                    {
                        var userPhone = req.PhoneNumber ?? string.Empty;
                        var userName = req.FullName ?? string.Empty;
                        await _notifications.NotifyAdminNewOrderAsync(order);
                        await _notifications.NotifyOrderCreatedAsync(order, email, userPhone, trackingUrl, userName);
                    }
                    catch
                    {
                        // Swallow notification errors to not fail the checkout flow
                    }

                return Ok(new { orderId = order.Id, amount = order.TotalAmount, currency = order.Currency, authorizationUrl = init.AuthorizationUrl, reference = init.Reference, trackingUrl });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> CreateFromCart([FromBody] CreateOrderFromCartRequest req)
        {
            try
            {
                var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
                if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var userId))
                {
                    return Unauthorized(new { message = "User ID is required for checkout." });
                }

                var address = new Address(req.FullName, req.Line1, req.Line2, req.City, req.State, req.Country, req.PostalCode, req.PhoneNumber);
                var email = (req.EmailForPayment ?? string.Empty).Trim();
                if (!_emailValidation.IsAcceptable(email))
                {
                    return BadRequest(new { message = "Invalid or unrecognized email provider.", recognizedProviders = _emailValidation.GetRecognizedDomains() });
                }

                var order = await _orders.CreateOrderFromCartAsync(userId, req.Cart, address, req.ExpectedDeliveryDate);

                var reference = $"ORD-{order.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                var init = await _paystack.InitializeTransactionAsync(order.TotalAmount, email, reference, req.CallbackUrl);
                await _orders.SetPaystackReferenceAsync(order.Id, init.Reference);

                var trackingUrl = $"{Request.Scheme}://{Request.Host}/api/orders/status?reference={init.Reference}";

                try
                {
                    var userPhone = req.PhoneNumber ?? string.Empty;
                    var userName = req.FullName ?? string.Empty;
                    await _notifications.NotifyAdminNewOrderAsync(order);
                    await _notifications.NotifyOrderCreatedAsync(order, email, userPhone, trackingUrl, userName);
                }
                catch
                {
                }

                return Ok(new { orderId = order.Id, amount = order.TotalAmount, currency = order.Currency, authorizationUrl = init.AuthorizationUrl, reference = init.Reference, trackingUrl });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ListMine([FromQuery] int skip = 0, [FromQuery] int take = 50)
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var userId))
            {
                // Anonymous-friendly: return empty list when user identity is not present
                return Ok(new List<OrderDto>());
            }
            var list = await _orders.ListOrdersForUserAsync(userId, skip, take);
            var result = new List<OrderDto>();
            foreach (var o in list)
            {
                var pay = await _orders.GetLatestPaymentForOrderAsync(o.Id);
                var items = new List<OrderItemDto>();
                foreach (var i in o.Items)
                {
                    items.Add(new OrderItemDto(i.NameSnapshot, i.Quantity, i.UnitPrice, i.Total));
                }
                result.Add(new OrderDto(o.Id, o.Status.ToString(), o.TotalAmount, o.Currency, o.CreatedAt, o.ExpectedDeliveryDate, items, pay == null ? null : new PaymentDto(pay.Reference, pay.Status, pay.PaidAt)));
            }
            return Ok(result);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetMine(Guid id)
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var userId))
            {
                // Anonymous-friendly: cannot resolve user; treat as not found
                return NotFound();
            }
            var o = await _orders.GetOrderForUserAsync(userId, id);
            if (o == null) return NotFound();
            var pay = await _orders.GetLatestPaymentForOrderAsync(o.Id);
            var items = new List<OrderItemDto>();
            foreach (var i in o.Items)
            {
                items.Add(new OrderItemDto(i.NameSnapshot, i.Quantity, i.UnitPrice, i.Total));
            }
            var dto = new OrderDto(o.Id, o.Status.ToString(), o.TotalAmount, o.Currency, o.CreatedAt, o.ExpectedDeliveryDate, items, pay == null ? null : new PaymentDto(pay.Reference, pay.Status, pay.PaidAt));
            return Ok(dto);
        }

        public record OrderStatusDto(Guid OrderId, string Status, decimal TotalAmount, string Currency, DateTime CreatedAt, DateTime? PaidAt);

        // Anonymous-friendly status polling by Paystack reference
        [HttpGet("status")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStatus([FromQuery] string reference)
        {
            if (string.IsNullOrWhiteSpace(reference)) return BadRequest(new { message = "Missing reference" });
            var o = await _orders.GetOrderByReferenceAsync(reference);
            if (o == null) return NotFound(new { message = "Order not found" });
            var pay = await _orders.GetLatestPaymentForOrderAsync(o.Id);
            var dto = new OrderStatusDto(o.Id, o.Status.ToString(), o.TotalAmount, o.Currency, o.CreatedAt, pay?.PaidAt);
            return Ok(dto);
        }
    }
}
