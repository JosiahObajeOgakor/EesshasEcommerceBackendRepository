using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Easshas.Domain.Entities;

namespace Easshas.Application.Abstractions
{
    public interface IContactMessageService
    {
        Task<ContactMessage> CreateAsync(ContactMessage message);
        Task<ContactMessage?> GetByIdAsync(Guid id);
        Task<List<ContactMessage>> ListAsync(int skip = 0, int take = 50);
        Task<int> GetCountAsync();
    }
}
