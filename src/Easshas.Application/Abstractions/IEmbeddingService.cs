using System.Threading.Tasks;

namespace Easshas.Application.Abstractions
{
    public interface IEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text);
    }
}
