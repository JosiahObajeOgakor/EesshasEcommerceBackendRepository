using System;
using System.Threading.Tasks;
using Easshas.Domain.Entities;

namespace Easshas.Application.Abstractions
{
    public interface IAdminSubscriptionService
    {
        Task<AdminSubscription> InitializeAsync(Guid adminUserId, decimal amount, string email, string callbackUrl);
        Task<bool> VerifyAndActivateAsync(string reference);
        Task<bool> IsActiveAsync(Guid adminUserId);
    }
}
