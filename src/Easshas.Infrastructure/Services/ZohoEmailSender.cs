using System;
using System.Text;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace Easshas.Infrastructure.Services
{
    public class ZohoEmailSender : IEmailSender
    {
        private readonly string _from;
        private readonly string _host;
        private readonly int _port;
        private readonly string _username;
        private readonly string _password;
        private readonly bool _useStartTls;
        private readonly IConfiguration _config;
        private readonly Easshas.Application.Abstractions.IProductService _productService;

        public ZohoEmailSender(IConfiguration configuration, Easshas.Application.Abstractions.IProductService productService)
        {
            _config = configuration;
            _from = configuration["Email:From"] ?? "no-reply@yourdomain.com";
            _host = configuration["Email:Smtp:Host"] ?? "smtp.zoho.com";
            _port = int.TryParse(configuration["Email:Smtp:Port"], out var p) ? p : 587;
            _username = configuration["Email:Smtp:Username"] ?? string.Empty;
            _password = configuration["Email:Smtp:Password"] ?? string.Empty;
            _useStartTls = string.Equals(configuration["Email:Smtp:UseStartTls"], "true", StringComparison.OrdinalIgnoreCase);
            _productService = productService;
        }

        private async Task SendHtmlAsync(string subject, string title, string contentHtml, string to, string? bcc = null)
        {
            var html = BrandedEmailTemplateHelper.GetBrandedHtml(_config, title, contentHtml);

            var message = new MimeMessage();
            // Allow multiple addresses separated by comma or semicolon; parse each entry safely.
            string NormalizeList(string s) => (s ?? string.Empty).Replace(';', ',').Trim();

            void TryAddFrom(string fromVal)
            {
                var list = NormalizeList(fromVal).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in list)
                {
                    var addr = part.Trim();
                    if (string.IsNullOrWhiteSpace(addr)) continue;
                    try
                    {
                        if (MailboxAddress.TryParse(addr, out var mbox))
                        {
                            message.From.Add(mbox);
                        }
                        else
                        {
                            // last resort
                            message.From.Add(MailboxAddress.Parse(addr));
                        }
                    }
                    catch
                    {
                        // skip invalid address
                    }
                }
            }

            void TryAddTo(string toVal)
            {
                var list = NormalizeList(toVal).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in list)
                {
                    var addr = part.Trim();
                    if (string.IsNullOrWhiteSpace(addr)) continue;
                    try
                    {
                        if (MailboxAddress.TryParse(addr, out var mbox))
                        {
                            message.To.Add(mbox);
                        }
                        else
                        {
                            message.To.Add(MailboxAddress.Parse(addr));
                        }
                    }
                    catch
                    {
                        // skip invalid address
                    }
                }
            }

            void TryAddBcc(string bccVal)
            {
                var list = NormalizeList(bccVal).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in list)
                {
                    var addr = part.Trim();
                    if (string.IsNullOrWhiteSpace(addr)) continue;
                    try
                    {
                        if (MailboxAddress.TryParse(addr, out var mbox))
                        {
                            message.Bcc.Add(mbox);
                        }
                        else
                        {
                            message.Bcc.Add(MailboxAddress.Parse(addr));
                        }
                    }
                    catch
                    {
                        // skip invalid address
                    }
                }
            }

            TryAddFrom(_from);
            // Ensure there's always at least one valid From address.
            if (message.From.Count == 0)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(_username) && MailboxAddress.TryParse(_username, out var mboxUser))
                    {
                        message.From.Add(mboxUser);
                    }
                    else if (!string.IsNullOrWhiteSpace(_from) && MailboxAddress.TryParse(_from, out var mboxFrom))
                    {
                        message.From.Add(mboxFrom);
                    }
                    else if (!string.IsNullOrWhiteSpace(_from))
                    {
                        // last resort: create a mailbox with display name and raw address
                        message.From.Add(new MailboxAddress("no-reply", _from));
                    }
                    else
                    {
                        message.From.Add(new MailboxAddress("no-reply", "no-reply@localhost"));
                    }
                }
                catch
                {
                    message.From.Add(new MailboxAddress("no-reply", "no-reply@localhost"));
                }
            }
            TryAddTo(to);
            if (!string.IsNullOrWhiteSpace(bcc)) TryAddBcc(bcc);

            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = html };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            // Pick socket option by port first (465 = implicit SSL), else honor UseStartTls when requested.
            SecureSocketOptions options;
            if (_port == 465)
                options = SecureSocketOptions.SslOnConnect;
            else if (_useStartTls)
                options = SecureSocketOptions.StartTls;
            else
                options = SecureSocketOptions.Auto;

            await client.ConnectAsync(_host, _port, options);
            if (!string.IsNullOrWhiteSpace(_username) && !string.IsNullOrWhiteSpace(_password))
            {
                await client.AuthenticateAsync(_username, _password);
            }
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendOrderConfirmationAsync(Order order, string userEmail, string adminEmail, string? userName = null)
        {
            var subject = $"Order Confirmation - {order.TrackingNumber}";
            var paymentRef = string.IsNullOrWhiteSpace(order.PaystackReference) ? "N/A" : order.PaystackReference;
            var greeting = !string.IsNullOrWhiteSpace(userName) ? $"Hi {userName}," : "Order Confirmation";

                        // Build user receipt content (modern, concise)
                        var userContent = $@"
                                <div style='padding:8px 4px'>
                                    <h2 style='margin-top:0'>{greeting}</h2>
                                    <p>Thank you for your order. Your payment has been received.</p>
                                    <h2 style='margin-top:20px'>Payment Receipt</h2>
                                    <div style='display:grid;grid-template-columns:1fr 1fr;gap:8px;align-items:center'>
                                        <div>
                                            <div style='font-size:12px;color:#6b7280'>Transaction Reference</div>
                                            <div style='font-weight:700'>{paymentRef}</div>
                                        </div>
                                        <div style='text-align:right'>
                                            <div style='font-size:12px;color:#6b7280'>Amount Paid</div>
                                            <div style='font-weight:700'>{order.TotalAmount} {order.Currency}</div>
                                            <div style='font-size:12px;color:#6b7280;margin-top:6px'>{DateTime.Now:yyyy-MM-dd HH:mm}</div>
                                        </div>
                                    </div>

                                    <div style='margin-top:12px;padding:12px;background:#f8fafc;border-radius:8px;border:1px solid #eef2f6'>
                                        <strong>Order ID:</strong> {order.Id}<br/>
                                        <strong>Tracking Number:</strong> {order.TrackingNumber}
                                    </div>

                                    <h3 style='margin-bottom:8px;margin-top:12px'>Items</h3>
                                    <table style='width:100%;border-collapse:collapse'>";

                        foreach (var item in order.Items)
                        {
                                userContent += $"<tr><td style='padding:8px;border-bottom:1px solid #f1f5f9'>{item.NameSnapshot}</td><td style='padding:8px;border-bottom:1px solid #f1f5f9;text-align:right'>x{item.Quantity}</td><td style='padding:8px;border-bottom:1px solid #f1f5f9;text-align:right'>{item.UnitPrice}</td></tr>";
                        }
                        userContent += "</table>";

                        var userAddr = order.BillingAddress;
                        userContent += $@"<h3 style='margin-top:12px'>Delivery Address</h3>
                                    <div style='font-size:14px'>{userAddr.FullName}<br/>{userAddr.Line1} {userAddr.Line2}<br/>{userAddr.City}, {userAddr.State} {userAddr.PostalCode}<br/>{userAddr.Country}</div>
                                </div>";

                        await SendHtmlAsync(subject, "Payment Received", userContent, userEmail);

                        // Build admin content: include inventory remaining by querying ProductService
                        var adminContent = $@"<div style='padding:8px 4px'>
                                <h2 style='margin-top:0'>New Order Processed</h2>
                                <p style='margin:0 0 8px 0;color:#374151'>Order {order.TrackingNumber} has been paid. See items and current remaining inventory below.</p>
                                <table style='width:100%;border-collapse:collapse'>
                                    <thead><tr><th style='text-align:left;padding:8px;border-bottom:2px solid #eef2f6'>Item</th><th style='text-align:right;padding:8px;border-bottom:2px solid #eef2f6'>Qty Ordered</th><th style='text-align:right;padding:8px;border-bottom:2px solid #eef2f6'>Remaining Inventory</th></tr></thead>
                                    <tbody>";

                        foreach (var item in order.Items)
                        {
                                try
                                {
                                        var prod = await _productService.GetByIdAsync(item.ProductId);
                                        var remaining = prod != null ? prod.Inventory.ToString() : "N/A";
                                        adminContent += $"<tr><td style='padding:8px;border-bottom:1px solid #f1f5f9'>{item.NameSnapshot}</td><td style='padding:8px;border-bottom:1px solid #f1f5f9;text-align:right'>{item.Quantity}</td><td style='padding:8px;border-bottom:1px solid #f1f5f9;text-align:right'>{remaining}</td></tr>";
                                }
                                catch
                                {
                                        adminContent += $"<tr><td style='padding:8px;border-bottom:1px solid #f1f5f9'>{item.NameSnapshot}</td><td style='padding:8px;border-bottom:1px solid #f1f5f9;text-align:right'>{item.Quantity}</td><td style='padding:8px;border-bottom:1px solid #f1f5f9;text-align:right'>N/A</td></tr>";
                                }
                        }

                        adminContent += "</tbody></table></div>";

                        await SendHtmlAsync($"Admin Notification - {order.TrackingNumber}", "Order Processed", adminContent, adminEmail);
        }

                    public async Task SendOrderCreatedAsync(Order order, string userEmail, string adminEmail, string trackingUrl, string? userName = null)
                    {
                        var subject = $"Order Received - {order.TrackingNumber}";
                        var greeting = !string.IsNullOrWhiteSpace(userName) ? $"Hi {userName}," : "Thank you for your order";

                        var userContent = $@"
                            <div style='padding:8px 4px'>
                                <h2 style='margin-top:0'>{greeting}</h2>
                                <p>We have received your order. It is currently awaiting payment. Use the button below to view or pay for your order.</p>
                                <div style='margin:18px 0;text-align:center'>
                                    <a href='{trackingUrl}' style='display:inline-block;padding:12px 20px;background:#111827;color:#fff;border-radius:8px;text-decoration:none;'>View & Pay for Order</a>
                                </div>
                                <div style='margin-top:12px;padding:12px;background:#f8fafc;border-radius:8px;border:1px solid #eef2f6'>
                                    <strong>Order ID:</strong> {order.Id}<br/>
                                    <strong>Tracking Number:</strong> {order.TrackingNumber}
                                </div>
                                <h3 style='margin-bottom:8px;margin-top:12px'>Items</h3>
                                <table style='width:100%;border-collapse:collapse'>";

                        foreach (var item in order.Items)
                        {
                            userContent += $"<tr><td style='padding:8px;border-bottom:1px solid #f1f5f9'>{item.NameSnapshot}</td><td style='padding:8px;border-bottom:1px solid #f1f5f9;text-align:right'>x{item.Quantity}</td><td style='padding:8px;border-bottom:1px solid #f1f5f9;text-align:right'>{item.UnitPrice}</td></tr>";
                        }
                        userContent += "</table>";

                        var addr = order.BillingAddress;
                        userContent += $@"<h3 style='margin-top:12px'>Delivery Address</h3>
                                <div style='font-size:14px'>{addr.FullName}<br/>{addr.Line1} {addr.Line2}<br/>{addr.City}, {addr.State} {addr.PostalCode}<br/>{addr.Country}</div>
                            </div>";

                        await SendHtmlAsync(subject, "Order Received", userContent, userEmail);

                        // Admin notification with same info
                        var adminContent = $@"<div style='padding:8px 4px'><h2>New Order (Awaiting Payment)</h2><p>Order {order.TrackingNumber} was created and is awaiting payment.</p></div>";
                        await SendHtmlAsync($"Admin: Order Created - {order.TrackingNumber}", "Order Created", adminContent, adminEmail);
                    }

        public async Task SendLowStockAlertAsync(Product product, string adminEmail)
        {
            var subject = $"⚠️ Low Stock Alert: {product.Name}";
            var content = $@"
                <p>The inventory for one of your products has reached a critical level.</p>
                <div style='background: #fff3cd; padding: 15px; border-radius: 5px; color: #856404; margin: 20px 0;'>
                    <p><strong>Product:</strong> {product.Name}</p>
                    <p><strong>SKU:</strong> {product.Sku}</p>
                    <p><strong>Remaining Stock:</strong> {product.Inventory}</p>
                </div>";

            await SendHtmlAsync(subject, "Inventory Warning", content, adminEmail);
        }

        public async Task SendLowStockWarningToUserAsync(Product product, string userEmail)
        {
            var subject = $"Hurry! {product.Name} is running out";
            var content = $@"
                <p>Thanks for your interest in <strong>{product.Name}</strong>!</p>
                <p>We wanted to let you know that we only have <strong>{product.Inventory}</strong> units left in stock.</p>
                <p>If you were planning to get another one, now would be a great time!</p>";

            await SendHtmlAsync(subject, "Limited Stock Available", content, userEmail);
        }

        public async Task SendOrderStatusChangedAsync(Order order, string userEmail, string status, string? note = null, string? userName = null)
        {
            var subject = $"Order #{order.TrackingNumber} Status Update: {status}";
            var greeting = !string.IsNullOrWhiteSpace(userName) ? $"Hi {userName}," : "Order Status Update";
            var content = $@"
                <p>{greeting}</p>
                <p>Your order status has been updated.</p>
                <div style='background: #e7f3ff; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <p><strong>New Status:</strong> {status}</p>
                    <p><strong>Tracking Number:</strong> {order.TrackingNumber}</p>
                </div>";

            if (!string.IsNullOrWhiteSpace(note))
            {
                content += $"<p><strong>Note:</strong> {note}</p>";
            }

            await SendHtmlAsync(subject, "Order Status Update", content, userEmail);
        }

        public async Task SendAdminNewOrderAlertAsync(Order order, string adminEmail)
        {
            var subject = $"🔔 New Order: #{order.TrackingNumber}";
            var content = $@"
                <p>A new order has been placed.</p>
                <div style='background: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                    <p><strong>Customer:</strong> {order.BillingAddress.FullName}</p>
                    <p><strong>Amount:</strong> {order.TotalAmount} {order.Currency}</p>
                </div>";

            await SendHtmlAsync(subject, "New Order Notification", content, adminEmail);
        }

        public async Task SendGenericEmailAsync(string to, string subject, string htmlContent, string? bcc = null)
        {
            await SendHtmlAsync(subject, subject, htmlContent, to, bcc);
        }
    }
}

