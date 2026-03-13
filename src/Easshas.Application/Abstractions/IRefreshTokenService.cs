using System;
using System.Threading.Tasks;

namespace Easshas.Application.Abstractions
{
    public interface IRefreshTokenService
    {
        Task<string> CreateAsync(Guid userId, int expiryMinutes);
        Task<(string newAccessToken, string newRefreshToken)> ValidateAndRotateAsync(string tokenValue, int accessExpiryMinutes, int refreshExpiryMinutes);
        Task RevokeAsync(string tokenValue);
    }
}
