using System;

namespace Easshas.Domain.Entities
{
    public class UserFact : BaseEntity
    {
        public Guid? UserId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string EmbeddingJson { get; set; } = string.Empty;
        public string? Tags { get; set; }
    }
}
