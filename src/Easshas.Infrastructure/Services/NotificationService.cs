
// ...no code above using directives...
using System;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Infrastructure.RealTime;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;

namespace Easshas.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IEmailSender _email;
        private readonly IWhatsAppNotifier _whatsapp;
        private readonly IHubContext<TrackingHub> _hubContext;
        private readonly IConfiguration _config;

        public NotificationService(IEmailSender email, IWhatsAppNotifier whatsapp, IHubContext<TrackingHub> hubContext, IConfiguration config)
        {
            _email = email;
            _whatsapp = whatsapp;
            _hubContext = hubContext;
            _config = config;
        }

        public async Task NotifyOrderPaidAsync(Order order, string userEmail, string userPhone, string? userName = null)
        {
            var adminEmail = _config["Email:Admin"] ?? "admin@example.com";
            var adminPhone = _config["WhatsApp:AdminPhone"] ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                await _email.SendOrderConfirmationAsync(order, userEmail, adminEmail, userName);
            }

            var waEnabled = string.Equals(_config["WhatsApp:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (waEnabled && !string.IsNullOrWhiteSpace(userPhone))
            {
                await _whatsapp.NotifyOrderConfirmedAsync(order, userPhone, adminPhone);
            }

            await _hubContext.Clients.Group("Admins").SendAsync("OrderPaid", new { orderId = order.Id, total = order.TotalAmount });
        }

        public async Task NotifyLowStockAsync(Product product)
        {
            var adminEmail = _config["Email:Admin"] ?? "admin@example.com";
            var adminPhone = _config["WhatsApp:AdminPhone"] ?? string.Empty;

            await _email.SendLowStockAlertAsync(product, adminEmail);

            var waEnabled = string.Equals(_config["WhatsApp:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (waEnabled && !string.IsNullOrWhiteSpace(adminPhone))
            {
                await _whatsapp.SendTextAsync(adminPhone, $"⚠️ LOW STOCK ALERT: {product.Name} (SKU: {product.Sku}) is at {product.Inventory} units.");
            }

            await _hubContext.Clients.All.SendAsync("InventoryAlert", new { productId = product.Id, sku = product.Sku, stock = product.Inventory });
        }

        public async Task NotifyAdminNewOrderAsync(Order order)
        {
            var adminEmail = _config["Email:Admin"] ?? "admin@example.com";
            var adminPhone = _config["WhatsApp:AdminPhone"] ?? string.Empty;
            var waEnabled = string.Equals(_config["WhatsApp:Enabled"], "true", StringComparison.OrdinalIgnoreCase);

            await _email.SendAdminNewOrderAlertAsync(order, adminEmail);

            if (waEnabled && !string.IsNullOrWhiteSpace(adminPhone))
            {
                await _whatsapp.SendTextAsync(adminPhone, $"🔔 NEW ORDER: Order #{order.Id} has been placed. Amount: {order.TotalAmount} {order.Currency}.");
            }

            await _hubContext.Clients.Group("Admins").SendAsync("NewOrder", new { orderId = order.Id });
        }

        public async Task NotifyOrderCreatedAsync(Order order, string userEmail, string userPhone, string trackingUrl, string? userName = null)
        {
            var adminEmail = _config["Email:Admin"] ?? "admin@example.com";
            // Send user-facing "order created / awaiting payment" email
            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                await _email.SendOrderCreatedAsync(order, userEmail, adminEmail, trackingUrl, userName);
            }

            // Optionally notify via WhatsApp
            var waEnabled = string.Equals(_config["WhatsApp:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (waEnabled && !string.IsNullOrWhiteSpace(userPhone))
            {
                await _whatsapp.SendTextAsync(userPhone, $"Your order {order.TrackingNumber} was created. Pay or track: {trackingUrl}");
            }

            // SignalR: notify admins a new pending order exists
            await _hubContext.Clients.Group("Admins").SendAsync("NewPendingOrder", new { orderId = order.Id, total = order.TotalAmount });
        }

        public async Task BroadcastProductUpdateAsync(Product product)
        {
            await _hubContext.Clients.All.SendAsync("ProductUpdated", new { product.Id, product.Name, product.Price, product.Available });
        }

        public async Task NotifyOrderStatusChangedAsync(Order order, string userEmail, string userPhone, string status, string? note = null, string? userName = null)
        {
            // Email
            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                await _email.SendOrderStatusChangedAsync(order, userEmail, status, note, userName);
            }

            // WhatsApp
            var waEnabled = string.Equals(_config["WhatsApp:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            if (waEnabled && !string.IsNullOrWhiteSpace(userPhone))
            {
                string message = $"Your order {order.TrackingNumber} status changed to {status}.";
                if (!string.IsNullOrWhiteSpace(note))
                    message += $" {note}";
                await _whatsapp.SendTextAsync(userPhone, message);
            }

            // SignalR (notify user group and admins)
            await _hubContext.Clients.Group($"order:{order.Id}").SendAsync("OrderStatusChanged", new { trackingNumber = order.TrackingNumber, status, note });
            await _hubContext.Clients.Group("Admins").SendAsync("OrderStatusChanged", new { trackingNumber = order.TrackingNumber, status, note });
        }
    }
}
