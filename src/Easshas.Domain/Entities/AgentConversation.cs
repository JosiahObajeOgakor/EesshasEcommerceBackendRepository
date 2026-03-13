using System;
using System.Collections.Generic;

namespace Easshas.Domain.Entities
{
    public class AgentConversation : BaseEntity
    {
        public Guid? UserId { get; set; }
        public string SessionId { get; set; } = string.Empty;
        public DateTime LastAt { get; set; } = DateTime.UtcNow;
        public List<AgentMessage> Messages { get; set; } = new();
    }
}
