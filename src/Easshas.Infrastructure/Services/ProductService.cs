using System;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Easshas.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly AppDbContext _db;
        public ProductService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<Product> CreateAsync(Product product)
        {
            _db.Products.Add(product);
            await _db.SaveChangesAsync();
            return product;
        }

        public Task<Product?> GetByIdAsync(Guid id)
        {
            return _db.Products.FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<Product?> UpdateAsync(Guid id, Product update)
        {
            var existing = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (existing == null) return null;
            existing.Name = update.Name;
            existing.Description = update.Description;
            existing.Category = update.Category;
            existing.BrandName = update.BrandName;
            existing.Price = update.Price;
            existing.Sku = update.Sku;
            existing.Inventory = update.Inventory;
            existing.Available = update.Available;
            existing.ImageUrl1 = update.ImageUrl1 ?? existing.ImageUrl1;
            existing.ImageUrl2 = update.ImageUrl2 ?? existing.ImageUrl2;
            existing.ImageUrl3 = update.ImageUrl3 ?? existing.ImageUrl3;
            existing.CategoryId = update.CategoryId;
            existing.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return existing;
        }

        public Task<Product?> GetByNameAsync(string name)
        {
            return _db.Products.FirstOrDefaultAsync(p => p.Name.ToLower() == name.ToLower());
        }

        public Task<Product?> GetBySkuAsync(string sku)
        {
            return _db.Products.FirstOrDefaultAsync(p => p.Sku != null && p.Sku.ToLower() == sku.ToLower());
        }

        public Task<System.Collections.Generic.List<Product>> ListAsync(int skip = 0, int take = 50)
        {
            return _db.Products
                .OrderBy(p => p.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public async Task<bool> CategoryExistsAsync(Guid categoryId)
        {
            return await _db.Categories.AnyAsync(c => c.Id == categoryId && c.Active);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return false;

            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            return true;
        }
    }
}
