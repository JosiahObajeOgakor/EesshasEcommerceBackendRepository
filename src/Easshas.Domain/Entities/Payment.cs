using System;

namespace Easshas.Domain.Entities
{
    public class Payment : BaseEntity
    {
        public Guid? UserId { get; set; }
        public Guid OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "NGN";
        public string Provider { get; set; } = "Paystack";
        public string Reference { get; set; } = default!;
        public string Status { get; set; } = "Pending"; // Pending, Success, Failed
        public DateTime? PaidAt { get; set; }
    }
}
