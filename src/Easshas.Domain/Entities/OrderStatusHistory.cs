using System;
using Easshas.Domain.Enums;

namespace Easshas.Domain.Entities
{
    public class OrderStatusHistory
    {
        public int Id { get; set; }
        public OrderStatus Status { get; set; }
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
        public string? Note { get; set; }
    }
}