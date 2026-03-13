using System;
using System.Collections.Generic;
using Easshas.Domain.Enums;
using Easshas.Domain.ValueObjects;

namespace Easshas.Domain.Entities
{
    public class Order : BaseEntity
    {
        public Guid UserId { get; set; }
        public List<OrderItem> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public Address BillingAddress { get; set; } = default!;
        public DateTime? ExpectedDeliveryDate { get; set; }
        public string Currency { get; set; } = "NGN";
        public string? PaystackReference { get; set; }

        // New fields for tracking
        public string TrackingNumber { get; set; } = string.Empty;
        public List<OrderStatusHistory> StatusHistory { get; set; } = new();
    }
}
