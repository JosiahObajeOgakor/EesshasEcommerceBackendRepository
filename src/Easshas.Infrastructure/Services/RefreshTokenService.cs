using System;
using System.Linq;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Infrastructure.Identity;
using Easshas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Easshas.Infrastructure.Services
{
    public class RefreshTokenService : IRefreshTokenService
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IJwtTokenService _jwt;
        public RefreshTokenService(AppDbContext db, UserManager<ApplicationUser> users, IJwtTokenService jwt)
        {
            _db = db;
            _users = users;
            _jwt = jwt;
        }

        public async Task<string> CreateAsync(Guid userId, int expiryMinutes)
        {
            var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var rt = new RefreshToken
            {
                UserId = userId,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes)
            };
            _db.RefreshTokens.Add(rt);
            await _db.SaveChangesAsync();
            return token;
        }

        public async Task<(string newAccessToken, string newRefreshToken)> ValidateAndRotateAsync(string tokenValue, int accessExpiryMinutes, int refreshExpiryMinutes)
        {
            var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == tokenValue);
            if (rt == null || !rt.IsActive)
            {
                throw new InvalidOperationException("Invalid or expired refresh token.");
            }
            var user = await _users.FindByIdAsync(rt.UserId.ToString());
            if (user == null)
            {
                throw new InvalidOperationException("User not found for refresh token.");
            }
            var roles = await _users.GetRolesAsync(user);
            var accessToken = await _jwt.GenerateTokenAsync(user.Id.ToString(), user.UserName!, roles);

            // revoke old and issue new
            rt.RevokedAt = DateTime.UtcNow;
            var newToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            var newRt = new RefreshToken
            {
                UserId = user.Id,
                Token = newToken,
                ExpiresAt = DateTime.UtcNow.AddMinutes(refreshExpiryMinutes)
            };
            _db.RefreshTokens.Add(newRt);
            await _db.SaveChangesAsync();
            return (accessToken, newToken);
        }

        public async Task RevokeAsync(string tokenValue)
        {
            if (string.IsNullOrWhiteSpace(tokenValue)) return;
            var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == tokenValue);
            if (rt == null) return;
            if (rt.RevokedAt == null)
            {
                rt.RevokedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
    }
}
