using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Easshas.Infrastructure.Services
{
    public class PgVectorStore : IVectorStore
    {
        private readonly AppDbContext _db;
        private readonly ILogger<PgVectorStore> _logger;

        public PgVectorStore(AppDbContext db, ILogger<PgVectorStore> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task IndexAsync(string docId, Guid? userId, string text, float[] embedding)
        {
            var ef = new Domain.Entities.UserFact { UserId = userId, Text = text, EmbeddingJson = JsonSerializer.Serialize(embedding) };
            _db.UserFacts.Add(ef);
            await _db.SaveChangesAsync();
        }

        public Task<List<string>> SearchAsync(Guid? userId, float[] queryEmbedding, int topK = 5)
        {
            // naive linear scan placeholder
            try
            {
                var facts = _db.UserFacts.Where(f => f.UserId == userId).ToList();
                var scored = new List<(Domain.Entities.UserFact fact, double score)>();
                foreach (var f in facts)
                {
                    try
                    {
                        var emb = JsonSerializer.Deserialize<float[]>(f.EmbeddingJson) ?? Array.Empty<float>();
                        var score = CosineSimilarity(queryEmbedding, emb);
                        scored.Add((f, score));
                    }
                    catch { }
                }
                var top = scored.OrderByDescending(s => s.score).Take(topK).Select(s => s.fact.Text).ToList();
                return Task.FromResult(top);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VectorStore search failed");
                return Task.FromResult(new List<string>());
            }
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0) return 0;
            var len = Math.Min(a.Length, b.Length);
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < len; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
            if (na == 0 || nb == 0) return 0;
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
        }
    }
}
