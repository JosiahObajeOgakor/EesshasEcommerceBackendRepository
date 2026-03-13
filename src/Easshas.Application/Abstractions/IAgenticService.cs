using System.Threading.Tasks;
using Easshas.Application.Models;

namespace Easshas.Application.Abstractions
{
    public interface IAgenticService
    {
        Task<AgenticResponse> HandleMessageAsync(string userId, string text, string? sessionId = null, string? language = "en");
    }
}
