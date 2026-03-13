using System;
using System.Linq;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Domain.ValueObjects;
using Easshas.Infrastructure.Persistence;
using Easshas.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Easshas.Tests
{
    // Using shared test fakes from TestFakes.cs

    public class OrderServiceTests
    {
        private AppDbContext CreateContext(string dbName)
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new AppDbContext(opts);
        }

        [Fact]
        public async Task CreateOrderFromCart_ReservesInventoryAndCreatesOrder()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var notif = new TestNotification();
            var svc = new OrderService(ctx, notif);

            var product = new Product { Id = Guid.NewGuid(), Name = "Test Lipstick", Price = 1000m, Inventory = 10, Available = true, BrandName = "BrandA", Category = "Makeup" };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();

            var cart = new CartDto();
            cart.Items.Add(new CartItemDto { ProductId = product.Id, Quantity = 3 });

            var addr = new Address("Alice", "Line1", null, "Lagos", "LA", "NG", "100001", "08000000000");

            var order = await svc.CreateOrderFromCartAsync(Guid.NewGuid(), cart, addr, null);

            Assert.Equal(1, ctx.Orders.Count());
            var p = await ctx.Products.FirstAsync(p => p.Id == product.Id);
            Assert.Equal(7, p.Inventory);
            Assert.True(order.TotalAmount == product.Price * 3);
        }
    }
}
