using System.Collections.Generic;
using System.Threading.Tasks;

namespace Easshas.Application.Abstractions
{
    public interface IJwtTokenService
    {
        Task<string> GenerateTokenAsync(string userId, string username, IEnumerable<string> roles);
    }
}
