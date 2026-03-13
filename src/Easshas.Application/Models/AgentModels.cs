using System;
using System.Collections.Generic;

namespace Easshas.Application.Models
{
    public enum AgentState { Idle, Planning, Executing, Evaluating, Retrying, Completed, Failed }

    public class AgentAction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object> Params { get; set; } = new();
        public int Attempt { get; set; } = 0;
        public int MaxAttempts { get; set; } = 3;
    }

    public class AgenticResponse
    {
        public AgentState State { get; set; }
        public string Reply { get; set; } = string.Empty;
        public List<AgentAction> Plan { get; set; } = new();
        public List<object>? ExecutionLog { get; set; }
    }
}
