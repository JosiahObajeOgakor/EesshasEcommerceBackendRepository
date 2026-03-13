using System;

namespace Easshas.Domain.Entities
{
    public class AdminSubscription : BaseEntity
    {
        public Guid AdminUserId { get; set; }
        public string PlanName { get; set; } = "WhatsApp-Notifications";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "NGN";
        public string Reference { get; set; } = default!;
        public string Status { get; set; } = "Pending"; // Pending, Active, Expired, Cancelled
        public DateTime? ActiveUntil { get; set; }
        public string Provider { get; set; } = "Paystack";
    }
}
