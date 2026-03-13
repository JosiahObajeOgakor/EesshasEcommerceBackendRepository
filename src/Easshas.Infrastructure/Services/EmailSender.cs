using System;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Easshas.Infrastructure.Secrets;

namespace Easshas.Infrastructure.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly string _from;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly bool _smtpSsl;

        public EmailSender(IConfiguration configuration)
        {
            _from = configuration["Email:From"] ?? "no-reply@example.com";
            _smtpHost = configuration["Email:SmtpHost"] ?? "";
            _smtpPort = int.TryParse(configuration["Email:SmtpPort"], out var port) ? port : 587;
            _smtpUser = configuration["Email:SmtpUser"] ?? "";
            _smtpPass = configuration["Email:SmtpPass"] ?? "";
            _smtpSsl = bool.TryParse(configuration["Email:SmtpSsl"], out var ssl) ? ssl : true;
        }

        // Removed AWS SES secret class

        public async Task SendOrderConfirmationAsync(Order order, string userEmail, string adminEmail, string? userName = null)
        {
            var subject = $"Order Confirmation - {order.Id}";
            var greeting = !string.IsNullOrWhiteSpace(userName) ? $"Hi {userName}," : "Order Confirmation";
            var html = $@"<html><body style='font-family:sans-serif;'>
                <h2>{greeting}</h2>
                <p>Thank you for your order. Your payment has been received.</p>
                <p><strong>Order Id:</strong> {order.Id}</p>
                <p><strong>Total:</strong> {order.TotalAmount} {order.Currency}</p>
                <p><strong>Status:</strong> {order.Status}</p>
                {(order.ExpectedDeliveryDate.HasValue ? $"<p><strong>Expected Delivery:</strong> {order.ExpectedDeliveryDate:yyyy-MM-dd}</p>" : "")}
                <h3>Items</h3>
                <ul>";
            foreach (var i in order.Items)
                html += $"<li>{i.NameSnapshot} x{i.Quantity} @ {i.UnitPrice}</li>";
            var addr = order.BillingAddress;
            html += $"</ul><h3>Billing Address</h3><p>{addr.FullName}, {addr.Line1} {addr.Line2}, {addr.City}, {addr.State} {addr.PostalCode}, {addr.Country}<br/>Phone: {addr.PhoneNumber}</p></body></html>";

            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _smtpSsl,
                Credentials = new NetworkCredential(_smtpUser, _smtpPass)
            };
            var mail = new MailMessage
            {
                From = new MailAddress(_from),
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            mail.To.Add(userEmail);
            mail.Bcc.Add(adminEmail);
            await smtp.SendMailAsync(mail);
        }

        public async Task SendOrderCreatedAsync(Order order, string userEmail, string adminEmail, string trackingUrl, string? userName = null)
        {
            var subject = $"Order Received - {order.TrackingNumber}";
            var greeting = !string.IsNullOrWhiteSpace(userName) ? $"Hi {userName}," : "Thank you for your order";
            var html = $@"<html><body style='font-family:sans-serif;'>
                <h2>{greeting}</h2>
                <p>We have received your order and it is awaiting payment. Click the button below to view or pay for your order.</p>
                <div style='margin:18px 0;text-align:center'><a href='{trackingUrl}' style='display:inline-block;padding:12px 20px;background:#111827;color:#fff;border-radius:8px;text-decoration:none;'>View & Pay for Order</a></div>
                <p><strong>Order ID:</strong> {order.Id}</p>
                <p><strong>Tracking Number:</strong> {order.TrackingNumber}</p>
                <h3>Items</h3><ul>";
            foreach (var i in order.Items)
                html += $"<li>{i.NameSnapshot} x{i.Quantity} @ {i.UnitPrice}</li>";
            html += $"</ul></body></html>";

            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _smtpSsl,
                Credentials = new System.Net.NetworkCredential(_smtpUser, _smtpPass)
            };
            var mail = new MailMessage
            {
                From = new MailAddress(_from),
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            mail.To.Add(userEmail);
            await smtp.SendMailAsync(mail);
        }

        public async Task SendLowStockAlertAsync(Product product, string adminEmail)
        {
            var subject = $"Low Stock Alert - {product.Name}";
            var html = $@"<html><body style='font-family:sans-serif;'>
                <h2>Low Stock Alert</h2>
                <p><strong>Product:</strong> {product.Name}</p>
                <p><strong>SKU:</strong> {product.Sku}</p>
                <p><strong>Current Inventory:</strong> {product.Inventory}</p>
                <p><strong>Status:</strong> {(product.Inventory <= 0 ? "OUT OF STOCK" : "LOW STOCK")}</p>
                </body></html>";

            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _smtpSsl,
                Credentials = new NetworkCredential(_smtpUser, _smtpPass)
            };
            var mail = new MailMessage
            {
                From = new MailAddress(_from),
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            mail.To.Add(adminEmail);
            await smtp.SendMailAsync(mail);
        }

        public async Task SendLowStockWarningToUserAsync(Product product, string userEmail)
        {
            var subject = $"Heads up: {product.Name} may run out soon";
            var html = $@"<html><body style='font-family:sans-serif;'>
                <h2>Heads up: {product.Name} may run out soon</h2>
                <p>Thanks for your purchase! <strong>{product.Name}</strong> is popular and stock is running low.</p>
                <p><strong>Remaining units:</strong> {product.Inventory}</p>
                <p>If you plan to order again, consider doing so soon.</p>
                </body></html>";

            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _smtpSsl,
                Credentials = new NetworkCredential(_smtpUser, _smtpPass)
            };
            var mail = new MailMessage
            {
                From = new MailAddress(_from),
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            mail.To.Add(userEmail);
            await smtp.SendMailAsync(mail);
        }

        public async Task SendOrderStatusChangedAsync(Order order, string userEmail, string status, string? note = null, string? userName = null)
        {
            var subject = $"Order Status Updated - {order.Id}";
            var greeting = !string.IsNullOrWhiteSpace(userName) ? $"Hi {userName}," : "Order Status Update";
            var noteSection = !string.IsNullOrWhiteSpace(note) ? $"<p><strong>Note:</strong> {note}</p>" : "";
            var html = $@"<html><body style='font-family:sans-serif;'>
                <h2>{greeting}</h2>
                <p><strong>Order Id:</strong> {order.Id}</p>
                <p><strong>New Status:</strong> {status ?? order.Status.ToString()}</p>
                <p><strong>Total:</strong> {order.TotalAmount} {order.Currency}</p>
                {noteSection}
                </body></html>";

            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _smtpSsl,
                Credentials = new NetworkCredential(_smtpUser, _smtpPass)
            };
            var mail = new MailMessage
            {
                From = new MailAddress(_from),
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            mail.To.Add(userEmail);
            await smtp.SendMailAsync(mail);
        }

        public async Task SendAdminNewOrderAlertAsync(Order order, string adminEmail)
        {
            var subject = $"New Order - {order.Id}";
            var html = $@"<html><body style='font-family:sans-serif;'>
                <h2>New Order Alert</h2>
                <p><strong>Order Id:</strong> {order.Id}</p>
                <p><strong>Total:</strong> {order.TotalAmount} {order.Currency}</p>
                <p><strong>Status:</strong> {order.Status}</p>
                <h3>Items</h3>
                <ul>";
            foreach (var i in order.Items)
                html += $"<li>{i.NameSnapshot} x{i.Quantity} @ {i.UnitPrice}</li>";
            html += $"</ul></body></html>";

            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _smtpSsl,
                Credentials = new NetworkCredential(_smtpUser, _smtpPass)
            };
            var mail = new MailMessage
            {
                From = new MailAddress(_from),
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            mail.To.Add(adminEmail);
            await smtp.SendMailAsync(mail);
        }

        public async Task SendGenericEmailAsync(string to, string subject, string htmlContent, string? bcc = null)
        {
            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                EnableSsl = _smtpSsl,
                Credentials = new NetworkCredential(_smtpUser, _smtpPass)
            };
            var mail = new MailMessage
            {
                From = new MailAddress(_from),
                Subject = subject,
                Body = htmlContent,
                IsBodyHtml = true
            };
            // support multiple recipients separated by comma or semicolon
            foreach (var addr in (to ?? string.Empty).Replace(';', ',').Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                mail.To.Add(addr.Trim());
            }
            if (!string.IsNullOrWhiteSpace(bcc))
            {
                foreach (var addr in bcc.Replace(';', ',').Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries))
                {
                    mail.Bcc.Add(addr.Trim());
                }
            }
            await smtp.SendMailAsync(mail);
        }
    }
}
