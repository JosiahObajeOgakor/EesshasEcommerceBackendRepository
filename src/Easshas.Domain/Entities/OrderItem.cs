using System;

namespace Easshas.Domain.Entities
{
    public class OrderItem
    {
        public Guid ProductId { get; set; }
        public string NameSnapshot { get; set; } = default!;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal Total => UnitPrice * Quantity;
    }
}
