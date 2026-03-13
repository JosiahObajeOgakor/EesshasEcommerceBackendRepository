using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Easshas.Domain.Entities;

namespace Easshas.Application.Abstractions
{
    public interface ICategoryService
    {
        Task<Category> CreateAsync(Category category);
        Task<Category?> GetByIdAsync(Guid id);
        Task<Category?> GetBySlugAsync(string slug);
        Task<IReadOnlyList<Category>> ListAsync(bool onlyActive = true);
        Task<Category?> UpdateAsync(Guid id, string name, bool? active = null);
    }
}
