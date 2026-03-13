using System;

namespace Easshas.Domain.Entities
{
    public class AgentMessage : BaseEntity
    {
        public Guid ConversationId { get; set; }
        public string Role { get; set; } = "user"; // user|agent|system
        public string Text { get; set; } = string.Empty;
        public string Language { get; set; } = "en";
    }
}
