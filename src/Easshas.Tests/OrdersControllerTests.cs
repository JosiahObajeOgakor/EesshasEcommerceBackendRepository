using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Domain.ValueObjects;
using Easshas.Infrastructure.Persistence;
using Easshas.Infrastructure.Services;
using Easshas.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Easshas.Tests
{
    // Use shared test fakes from TestFakes.cs

    public class OrdersControllerTests
    {
        private AppDbContext CreateContext(string dbName)
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new AppDbContext(opts);
        }

        [Fact]
        public async Task CheckoutFromCart_ControllerCreatesOrderAndReservesInventory()
        {
            var ctx = CreateContext(Guid.NewGuid().ToString());
            var notif = new FakeNotificationService();
            var orderService = new OrderService(ctx, notif);
            var paystack = new FakePaystack();
            var emailValidation = new FakeEmailValidation();

            var controller = new OrdersController(orderService, paystack, emailValidation, notif);

            // seed product
            var product = new Product { Id = Guid.NewGuid(), Name = "Test Lipstick", Price = 1200m, Inventory = 5, Available = true, BrandName = "B", Category = "Makeup" };
            ctx.Products.Add(product);
            await ctx.SaveChangesAsync();

            // set user on controller
            var userId = Guid.NewGuid();
            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"));
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = claims } };

            var cart = new CartDto();
            cart.Items.Add(new CartItemDto { ProductId = product.Id, Quantity = 2 });

            var req = new OrdersController.CreateOrderFromCartRequest(cart, "Bob", "Line1", null, "Lagos", "LA", "NG", "100001", "08009990000", null, "bob@example.com", "https://example/callback");

            var res = await controller.CreateFromCart(req) as OkObjectResult;
            Assert.NotNull(res);
            var body = res.Value as dynamic;
            Assert.Equal("https://pay.test/authorize", (string)body.authorizationUrl);

            var p = await ctx.Products.FirstAsync(p => p.Id == product.Id);
            Assert.Equal(3, p.Inventory); // reserved 2
        }
    }
}
