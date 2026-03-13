using System.Collections.Generic;
using System.Threading.Tasks;

namespace Easshas.Application.Abstractions
{
    public class AgentIntent
    {
        public string Intent { get; set; } = "unknown";
        public string? ProductName { get; set; }
        public string? Language { get; set; }
        public int Quantity { get; set; } = 1;
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? RawQuery { get; set; }
        public Dictionary<string, string> Entities { get; set; } = new();
    }

    public interface ICommerceAgent
    {
        Task<AgentIntent> ProcessQueryAsync(string query, string? context = null);
        Task<string> GenerateResponseAsync(AgentIntent intent, string? additionalInfo = null);
    }
}
