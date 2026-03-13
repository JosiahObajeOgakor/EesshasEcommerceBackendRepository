using System.Threading.Tasks;
using Easshas.Domain.Entities;

namespace Easshas.Application.Abstractions
{
    public interface IChatSessionService
    {
        Task<ChatSession> GetOrCreateAsync(string phone);
        Task SaveAsync(ChatSession session);
    }
}
