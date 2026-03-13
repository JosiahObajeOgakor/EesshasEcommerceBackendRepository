using System;

namespace Easshas.Domain.Entities
{
    public class ContactMessage : BaseEntity
    {
        public string Name { get; set; } = default!;
        public string Message { get; set; } = default!;
        public string PhoneNumber { get; set; } = default!;
    }
}
