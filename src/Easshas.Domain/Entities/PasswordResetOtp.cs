using System;

namespace Easshas.Domain.Entities
{
    public class PasswordResetOtp : BaseEntity
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Otp { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; } = false;
    }
}
