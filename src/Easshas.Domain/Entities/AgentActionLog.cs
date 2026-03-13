using System;

namespace Easshas.Domain.Entities
{
    public class AgentActionLog : BaseEntity
    {
        public Guid ConversationId { get; set; }
        public string ActionId { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Success, Failed
        public string ResultJson { get; set; } = string.Empty;
        public int Attempts { get; set; }
    }
}
