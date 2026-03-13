using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Easshas.Application.Abstractions;
using Easshas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Easshas.WebApi.Services
{
    public class PaymentReconciliationService : BackgroundService
    {
        private readonly ILogger<PaymentReconciliationService> _logger;
        private readonly IServiceProvider _services;
        private readonly IConfiguration _configuration;

        public PaymentReconciliationService(ILogger<PaymentReconciliationService> logger, IServiceProvider services, IConfiguration configuration)
        {
            _logger = logger;
            _services = services;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var enabled = string.Equals(_configuration["App:Reconciliation:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
            var intervalSeconds = int.TryParse(_configuration["App:Reconciliation:IntervalSeconds"], out var i) ? i : 60;
            var minAgeMinutes = int.TryParse(_configuration["App:Reconciliation:MinAgeMinutes"], out var m) ? m : 5;

            if (!enabled)
            {
                _logger.LogInformation("Payment reconciliation disabled.");
                return;
            }

            _logger.LogInformation("Payment reconciliation started: interval={Interval}s, minAge={MinAge}m", intervalSeconds, minAgeMinutes);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var orders = scope.ServiceProvider.GetRequiredService<IOrderService>();
                    var paystack = scope.ServiceProvider.GetRequiredService<IPaystackService>();
                    var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();
                    var whatsapp = scope.ServiceProvider.GetRequiredService<IWhatsAppNotifier>();
                    var subs = scope.ServiceProvider.GetRequiredService<IAdminSubscriptionService>();
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                    var pending = await orders.ListPendingOrdersAsync(minAgeMinutes);
                    foreach (var order in pending)
                    {
                        if (string.IsNullOrWhiteSpace(order.PaystackReference)) continue;
                        var reference = order.PaystackReference;
                        var ok = await paystack.VerifyTransactionAsync(reference);
                        if (!ok) continue;

                        var marked = await orders.MarkOrderPaidAsync(reference);
                        var refreshed = await orders.GetOrderByReferenceAsync(reference);
                        if (refreshed == null) continue;

                        var adminEmail = _configuration["Email:Admin"] ?? "admin@example.com";
                        var adminPhone = _configuration["WhatsApp:AdminPhone"] ?? string.Empty;
                        var user = await userManager.FindByIdAsync(refreshed.UserId.ToString());
                        var userEmail = user?.Email ?? string.Empty;
                        var userPhone = user?.PhoneNumber ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(userEmail))
                        {
                            await email.SendOrderConfirmationAsync(refreshed, userEmail, adminEmail);
                        }

                        var waEnabled = string.Equals(_configuration["WhatsApp:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
                        var requireSub = string.Equals(_configuration["WhatsApp:RequireSubscription"], "true", StringComparison.OrdinalIgnoreCase);
                        var canNotify = waEnabled;
                        if (requireSub)
                        {
                            var adminEmailUser = _configuration["Admin:Email"] ?? _configuration["Email:Admin"];
                            Guid adminId = Guid.Empty;
                            if (!string.IsNullOrWhiteSpace(adminEmailUser))
                            {
                                var adminUser = await userManager.FindByEmailAsync(adminEmailUser);
                                if (adminUser != null) Guid.TryParse(adminUser.Id.ToString(), out adminId);
                            }
                            if (adminId != Guid.Empty)
                            {
                                canNotify = canNotify && await subs.IsActiveAsync(adminId);
                            }
                        }
                        if (canNotify && !string.IsNullOrWhiteSpace(userPhone) && !string.IsNullOrWhiteSpace(adminPhone))
                        {
                            await whatsapp.NotifyOrderConfirmedAsync(refreshed, userPhone, adminPhone);
                        }

                        _logger.LogInformation("Reconciled order {OrderId} via reference {Reference}", refreshed.Id, reference);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during payment reconciliation");
                }
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
        }
    }
}
