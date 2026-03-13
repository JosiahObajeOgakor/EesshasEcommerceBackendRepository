using System;
using System.Linq;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PayStack.Net;

namespace Easshas.Infrastructure.Services
{
    public class AdminSubscriptionService : IAdminSubscriptionService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _configuration;

        public AdminSubscriptionService(AppDbContext db, IConfiguration configuration)
        {
            _db = db;
            _configuration = configuration;
        }

        public async Task<AdminSubscription> InitializeAsync(Guid adminUserId, decimal amount, string email, string callbackUrl)
        {
            var secret = _configuration["Paystack:Subscriptions:SecretKey"] ?? _configuration["Paystack:SecretKey"] ?? string.Empty;
            var api = new PayStackApi(secret);
            var reference = $"SUB-{adminUserId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            var initReq = new TransactionInitializeRequest
            {
                AmountInKobo = (int)(amount * 100),
                Email = email,
                Reference = reference,
                CallbackUrl = callbackUrl,
                Currency = "NGN"
            };
            var resp = api.Transactions.Initialize(initReq);
            if (!resp.Status) throw new InvalidOperationException($"Init failed: {resp.Message}");

            var sub = new AdminSubscription
            {
                AdminUserId = adminUserId,
                Amount = amount,
                Currency = "NGN",
                Reference = resp.Data.Reference,
                Status = "Pending",
                Provider = "Paystack"
            };
            _db.AdminSubscriptions.Add(sub);
            await _db.SaveChangesAsync();
            return sub;
        }

        public async Task<bool> VerifyAndActivateAsync(string reference)
        {
            var secret = _configuration["Paystack:Subscriptions:SecretKey"] ?? _configuration["Paystack:SecretKey"] ?? string.Empty;
            var api = new PayStackApi(secret);
            var verify = api.Transactions.Verify(reference);
            if (!verify.Status || !string.Equals(verify.Data.Status, "success", StringComparison.OrdinalIgnoreCase)) return false;

            var sub = await _db.AdminSubscriptions.FirstOrDefaultAsync(s => s.Reference == reference);
            if (sub == null) return false;
            sub.Status = "Active";
            sub.ActiveUntil = DateTime.UtcNow.AddMonths(1);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsActiveAsync(Guid adminUserId)
        {
            var now = DateTime.UtcNow;
            return await _db.AdminSubscriptions.AnyAsync(s => s.AdminUserId == adminUserId && s.Status == "Active" && s.ActiveUntil != null && s.ActiveUntil > now);
        }
    }
}
