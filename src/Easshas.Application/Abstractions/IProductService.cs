using System;
using System.Threading.Tasks;
using Easshas.Domain.Entities;

namespace Easshas.Application.Abstractions
{
    public interface IProductService
    {
        Task<Product> CreateAsync(Product product);
        Task<Product?> GetByIdAsync(Guid id);
        Task<Product?> UpdateAsync(Guid id, Product update);
        Task<Product?> GetByNameAsync(string name);
        Task<Product?> GetBySkuAsync(string sku);
        Task<System.Collections.Generic.List<Product>> ListAsync(int skip = 0, int take = 50);
        Task<bool> CategoryExistsAsync(Guid categoryId);
        Task<bool> DeleteAsync(Guid id);
    }
}
