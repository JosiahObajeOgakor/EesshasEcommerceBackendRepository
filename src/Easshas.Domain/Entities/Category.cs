using System;
using System.Collections.Generic;

namespace Easshas.Domain.Entities
{
    public class Category : BaseEntity
    {
        public string Name { get; set; } = default!;
        public string Slug { get; set; } = default!;
        public bool Active { get; set; } = true;

        public ICollection<Product> Products { get; set; } = new List<Product>();
    }
}
