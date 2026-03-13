using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;

namespace Easshas.Infrastructure.Services
{
    public class CartService : ICartService
    {
        // Stub implementation for build
        public Task AddToCartAsync(Guid userId, Guid productId, int quantity)
        {
            // TODO: Implement cart persistence
            return Task.CompletedTask;
        }
        public Task<CartDto> GetCartAsync(Guid userId)
        {
            // TODO: Implement cart retrieval
            return Task.FromResult(new CartDto());
        }
    }
}
