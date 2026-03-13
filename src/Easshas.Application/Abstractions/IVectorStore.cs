using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Easshas.Application.Abstractions
{
    public interface IVectorStore
    {
        Task IndexAsync(string docId, Guid? userId, string text, float[] embedding);
        Task<List<string>> SearchAsync(Guid? userId, float[] queryEmbedding, int topK = 5);
    }
}
