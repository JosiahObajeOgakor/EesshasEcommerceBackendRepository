using System;

namespace Easshas.Domain.Entities
{
    public class ChatSession : BaseEntity
    {
        public Guid? UserId { get; set; }
        public string Phone { get; set; } = default!;
        public string State { get; set; } = "Idle";
        public Guid? ProductId { get; set; }
        public int? Quantity { get; set; }
        public string? EmailForPayment { get; set; }
        public string? FullName { get; set; }
        public string? Line1 { get; set; }
        public string? Line2 { get; set; }
        public string? City { get; set; }
        public string? StateRegion { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public Guid? OrderId { get; set; }
        public string? PaystackReference { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
