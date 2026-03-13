using System.Threading.Tasks;

namespace Easshas.Application.Abstractions
{
    public class PaystackInitResult
    {
        public string AuthorizationUrl { get; set; } = default!;
        public string Reference { get; set; } = default!;
    }

    public interface IPaystackService
    {
        Task<PaystackInitResult> InitializeTransactionAsync(decimal amount, string email, string reference, string callbackUrl);
        Task<bool> VerifyTransactionAsync(string reference);
    }
}
