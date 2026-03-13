using System;

namespace Easshas.Domain.Entities
{
    public class RefreshToken : BaseEntity
    {
        public Guid UserId { get; set; }
        public string Token { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;
    }
}
