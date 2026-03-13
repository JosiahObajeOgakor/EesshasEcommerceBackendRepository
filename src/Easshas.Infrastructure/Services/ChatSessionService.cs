using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Easshas.Infrastructure.Services
{
    public class ChatSessionService : IChatSessionService
    {
        private readonly AppDbContext _db;
        public ChatSessionService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<ChatSession> GetOrCreateAsync(string phone)
        {
            var s = await _db.ChatSessions.FirstOrDefaultAsync(c => c.Phone == phone);
            if (s != null) return s;
            s = new ChatSession { Phone = phone, State = "Idle" };
            _db.ChatSessions.Add(s);
            await _db.SaveChangesAsync();
            return s;
        }

        public async Task SaveAsync(ChatSession session)
        {
            session.LastUpdated = System.DateTime.UtcNow;
            _db.Update(session);
            await _db.SaveChangesAsync();
        }
    }
}
