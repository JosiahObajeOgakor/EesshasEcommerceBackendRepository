using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Easshas.Application.Abstractions
{
    public interface ICartService
    {
        Task AddToCartAsync(Guid userId, Guid productId, int quantity);
        Task<CartDto> GetCartAsync(Guid userId);
    }

    public class CartDto
    {
        public List<CartItemDto> Items { get; set; } = new();
    }
    public class CartItemDto
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }
}
