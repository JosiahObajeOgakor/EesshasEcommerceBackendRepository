using System.Threading.Tasks;

namespace Easshas.Application.Abstractions
{
    public interface IConversationService
    {
        Task<string> HandleMessageAsync(string fromPhone, string text);
    }
}
