using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Easshas.Infrastructure.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly AppDbContext _db;
        public CategoryService(AppDbContext db) { _db = db; }

        public async Task<Category> CreateAsync(Category category)
        {
            // ensure unique slug
            if (await _db.Categories.AnyAsync(c => c.Slug.ToLower() == category.Slug.ToLower()))
                throw new InvalidOperationException("Category slug already exists.");
            _db.Categories.Add(category);
            await _db.SaveChangesAsync();
            return category;
        }

        public Task<Category?> GetByIdAsync(Guid id) => _db.Categories.FirstOrDefaultAsync(c => c.Id == id);
        public Task<Category?> GetBySlugAsync(string slug) => _db.Categories.FirstOrDefaultAsync(c => c.Slug.ToLower() == slug.ToLower());
        public async Task<IReadOnlyList<Category>> ListAsync(bool onlyActive = true)
        {
            var q = _db.Categories.AsQueryable();
            if (onlyActive) q = q.Where(c => c.Active);
            return await q.OrderBy(c => c.Name).ToListAsync();
        }

        public async Task<Category?> UpdateAsync(Guid id, string name, bool? active = null)
        {
            var c = await _db.Categories.FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return null;
            c.Name = name;
            if (active.HasValue) c.Active = active.Value;
            await _db.SaveChangesAsync();
            return c;
        }
    }
}
