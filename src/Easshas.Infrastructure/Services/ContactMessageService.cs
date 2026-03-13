using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Easshas.Infrastructure.Services
{
    public class ContactMessageService : IContactMessageService
    {
        private readonly AppDbContext _db;

        public ContactMessageService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<ContactMessage> CreateAsync(ContactMessage message)
        {
            _db.ContactMessages.Add(message);
            await _db.SaveChangesAsync();
            return message;
        }

        public Task<ContactMessage?> GetByIdAsync(Guid id)
        {
            return _db.ContactMessages.FirstOrDefaultAsync(m => m.Id == id);
        }

        public Task<List<ContactMessage>> ListAsync(int skip = 0, int take = 50)
        {
            return _db.ContactMessages
                .OrderByDescending(m => m.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public Task<int> GetCountAsync()
        {
            return _db.ContactMessages.CountAsync();
        }
    }
}
