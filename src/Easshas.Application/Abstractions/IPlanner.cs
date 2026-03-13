using System.Collections.Generic;
using System.Threading.Tasks;
using Easshas.Application.Models;

namespace Easshas.Application.Abstractions
{
    public interface IPlanner
    {
        Task<List<AgentAction>> PlanAsync(string userId, string text, string? language = "en");
    }
}
