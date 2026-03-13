using System;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;

namespace Easshas.Tests
{
    public class TestNotification : INotificationService
    {
        public Task BroadcastProductUpdateAsync(Product product) => Task.CompletedTask;
        public Task NotifyAdminNewOrderAsync(Order order) => Task.CompletedTask;
        public Task NotifyLowStockAsync(Product product) => Task.CompletedTask;
        public Task NotifyOrderCreatedAsync(Order order, string userEmail, string userPhone, string trackingUrl, string? userName = null) => Task.CompletedTask;
        public Task NotifyOrderPaidAsync(Order order, string userEmail, string userPhone, string? userName = null) => Task.CompletedTask;
        public Task NotifyOrderStatusChangedAsync(Order order, string userEmail, string userPhone, string status, string? note = null, string? userName = null) => Task.CompletedTask;
    }

    // Compatibility fakes expected by older tests
    public class FakeNotificationService : TestNotification { }

    public class TestPaystack : IPaystackService
    {
        public Task<PaystackInitResult> InitializeTransactionAsync(decimal amount, string email, string reference, string callbackUrl)
        {
            return Task.FromResult(new PaystackInitResult { AuthorizationUrl = "https://pay.test/authorize", Reference = reference });
        }
        public Task<bool> VerifyTransactionAsync(string reference) => Task.FromResult(true);
    }

    public class FakePaystack : TestPaystack { }

    public class TestEmailValidation : IEmailValidationService
    {
        public bool IsValidFormat(string email) => true;
        public bool IsRecognizedDomain(string email) => true;
        public bool IsDisposableDomain(string email) => false;
        public bool IsAcceptable(string email) => true;
        public System.Collections.Generic.IReadOnlyList<string> GetRecognizedDomains() => new[] { "example.com" };
    }

    public class FakeEmailValidation : TestEmailValidation { }
    
    public class TestEmailSender : Easshas.Application.Abstractions.IEmailSender
    {
        public System.Collections.Concurrent.ConcurrentBag<(string To, string Subject, string Body)> Sent = new();

        public Task SendAdminNewOrderAlertAsync(Order order, string adminEmail)
        {
            return Task.CompletedTask;
        }

        public Task SendLowStockAlertAsync(Product product, string adminEmail)
        {
            return Task.CompletedTask;
        }

        public Task SendLowStockWarningToUserAsync(Product product, string userEmail)
        {
            return Task.CompletedTask;
        }

        public Task SendOrderConfirmationAsync(Order order, string userEmail, string adminEmail, string? userName = null)
        {
            return Task.CompletedTask;
        }

        public Task SendOrderCreatedAsync(Order order, string userEmail, string adminEmail, string trackingUrl, string? userName = null)
        {
            return Task.CompletedTask;
        }

        public Task SendOrderStatusChangedAsync(Order order, string userEmail, string status, string? note = null, string? userName = null)
        {
            Sent.Add((userEmail, status ?? string.Empty, note ?? string.Empty));
            return Task.CompletedTask;
        }

        public Task SendGenericEmailAsync(string to, string subject, string htmlContent, string? bcc = null)
        {
            Sent.Add((to, subject, htmlContent));
            return Task.CompletedTask;
        }
    }
}
