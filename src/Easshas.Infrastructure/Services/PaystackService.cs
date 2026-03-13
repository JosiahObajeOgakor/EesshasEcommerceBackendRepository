using System;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using PayStack.Net;

namespace Easshas.Infrastructure.Services
{
    public class PaystackService : IPaystackService
    {
        private readonly string _secretKey;

        public PaystackService(IConfiguration configuration)
        {
            _secretKey = configuration["Paystack:SecretKey"] ?? string.Empty;
        }

        public Task<PaystackInitResult> InitializeTransactionAsync(decimal amount, string email, string reference, string callbackUrl)
        {
            var api = new PayStackApi(_secretKey);
            // Convert amount (NGN) to kobo safely, avoiding overflow and rounding issues
            var koboDecimal = decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
            if (koboDecimal <= 0)
            {
                throw new InvalidOperationException("Invalid amount. Must be greater than 0.");
            }
            if (koboDecimal > int.MaxValue)
            {
                throw new InvalidOperationException("Amount too large for Paystack. Please reduce order total.");
            }
            var amountInKobo = (int)koboDecimal;
            var initReq = new TransactionInitializeRequest
            {
                AmountInKobo = amountInKobo,
                Email = email,
                Reference = reference,
                CallbackUrl = callbackUrl,
                Currency = "NGN"
            };
            var response = api.Transactions.Initialize(initReq);
            if (!response.Status)
            {
                throw new InvalidOperationException($"Paystack init failed: {response.Message}");
            }
            return Task.FromResult(new PaystackInitResult
            {
                AuthorizationUrl = response.Data.AuthorizationUrl,
                Reference = response.Data.Reference
            });
        }

        public Task<bool> VerifyTransactionAsync(string reference)
        {
            var api = new PayStackApi(_secretKey);
            var verify = api.Transactions.Verify(reference);
            return Task.FromResult(verify.Status && verify.Data.Status.Equals("success", StringComparison.OrdinalIgnoreCase));
        }
    }
}
