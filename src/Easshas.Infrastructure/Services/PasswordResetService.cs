using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Infrastructure.Identity;
using Easshas.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;

namespace Easshas.Infrastructure.Services
{
    public class PasswordResetService : IPasswordResetService
    {
        private readonly AppDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _config;
        private readonly ILogger<PasswordResetService> _logger;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

        public PasswordResetService(AppDbContext db, UserManager<ApplicationUser> userManager, IEmailSender emailSender, IConfiguration config, ILogger<PasswordResetService> logger, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
        {
            _db = db;
            _userManager = userManager;
            _emailSender = emailSender;
            _config = config;
            _logger = logger;
            _cache = cache;
        }

        public async Task<bool> SendResetOtpAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user == null) return false;

            // Rate-limit OTP sends per email (default 3 per hour)
            var sendLimit = _config.GetValue<int?>("App:PasswordReset:SendLimitPerWindow") ?? 3;
            var windowMinutes = _config.GetValue<int?>("App:PasswordReset:SendWindowMinutes") ?? 60;
            var sendKey = $"pwdreset:send:{email.Trim().ToLowerInvariant()}";
            var current = _cache.Get<int>(sendKey);
            if (current >= sendLimit)
            {
                _logger.LogWarning("OTP send rate-limit exceeded for {Email}", email);
                return false;
            }

            // Generate 6-digit OTP
            var otp = GenerateOtp(6);
            var expires = DateTime.UtcNow.AddMinutes(_config.GetValue<int?>("App:PasswordReset:OtpExpiryMinutes") ?? 15);

            var entry = new PasswordResetOtp
            {
                UserId = user.Id,
                Email = user.Email ?? string.Empty,
                Otp = otp,
                ExpiresAt = expires,
                Used = false
            };

            _db.PasswordResetOtps.Add(entry);
            await _db.SaveChangesAsync();

            // increment send counter
            _cache.Set(sendKey, current + 1, new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(windowMinutes)
            });

            try
            {
                // Send OTP email via IEmailSender - best-effort
                var subject = "Your password reset code";
                var content = $"<p>Your password reset code is: <strong>{otp}</strong></p><p>It expires at {expires:HH:mm} UTC.</p>";
                await _emailSender.SendGenericEmailAsync(user.Email ?? string.Empty, subject, content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send OTP email via IEmailSender");
            }

            return true;
        }

        public async Task<bool> VerifyOtpAndResetPasswordAsync(string email, string otp, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otp) || string.IsNullOrWhiteSpace(newPassword))
                return false;
            // Rate-limit OTP verification attempts per email (default 5 per hour)
            var verifyLimit = _config.GetValue<int?>("App:PasswordReset:VerifyLimitPerWindow") ?? 5;
            var verifyWindow = _config.GetValue<int?>("App:PasswordReset:VerifyWindowMinutes") ?? 60;
            var verifyKey = $"pwdreset:verify:{email.Trim().ToLowerInvariant()}";
            var vCurrent = _cache.Get<int>(verifyKey);
            if (vCurrent >= verifyLimit)
            {
                _logger.LogWarning("OTP verify rate-limit exceeded for {Email}", email);
                return false;
            }
            var now = DateTime.UtcNow;
            var record = await _db.PasswordResetOtps
                .Where(o => o.Email == email.Trim() && !o.Used && o.ExpiresAt >= now && o.Otp == otp)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();

            if (record == null)
            {
                IncrementVerifyAttempt(email, (int)verifyWindow);
                return false;
            }

            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user == null)
            {
                IncrementVerifyAttempt(email, (int)verifyWindow);
                return false;
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
            {
                IncrementVerifyAttempt(email, (int)verifyWindow);
                return false;
            }

            record.Used = true;
            await _db.SaveChangesAsync();

            // reset/clear verify attempts on success
            _cache.Remove(verifyKey);

            return true;
        }

        // increment verify attempts on failure
        private void IncrementVerifyAttempt(string email, int windowMinutes)
        {
            try
            {
                var key = $"pwdreset:verify:{email.Trim().ToLowerInvariant()}";
                var curr = _cache.Get<int>(key);
                _cache.Set(key, curr + 1, new Microsoft.Extensions.Caching.Memory.MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(windowMinutes)
                });
            }
            catch { }
        }

        private static string GenerateOtp(int digits)
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[4];
            rng.GetBytes(bytes);
            var value = Math.Abs(BitConverter.ToInt32(bytes, 0)) % (int)Math.Pow(10, digits);
            return value.ToString($"D{digits}");
        }
    }
}
