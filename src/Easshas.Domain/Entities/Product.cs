using Easshas.Domain.Entities;

namespace Easshas.Domain.Entities
{
    public class Product : BaseEntity
    {
        public string Name { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string Category { get; set; } = default!;
        public decimal Price { get; set; }
        public string BrandName { get; set; } = default!;
        public bool Active { get; set; } = true;

        public string? Sku { get; set; }
        public int Inventory { get; set; }
        public bool Available { get; set; } = true;
        public string? ImageUrl1 { get; set; }
        public string? ImageUrl2 { get; set; }
        public string? ImageUrl3 { get; set; }

        public System.Guid? CategoryId { get; set; }
        public Category? CategoryRef { get; set; }
    }
}
