using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Domain.ValueObjects;
using Easshas.Infrastructure.Persistence;
using Easshas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Internal;
using Xunit;

namespace Easshas.Tests.Integration
{
    // Test auth handler that injects a user id claim
    public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder, Microsoft.AspNetCore.Authentication.ISystemClock clock)
            : base(options, logger, encoder, clock) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class CustomFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((context, conf) =>
            {
                var dict = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["App:UseInMemory"] = "true"
                };
                conf.AddInMemoryCollection(dict);
            });

            builder.ConfigureServices(services =>
            {
                // Replace Paystack, Notification, EmailValidation with test fakes
                services.AddSingleton<IPaystackService, TestPaystack>();
                services.AddSingleton<IEmailValidationService, TestEmailValidation>();
                services.AddSingleton<INotificationService, TestNotification>();
                    services.AddSingleton<Easshas.Application.Abstractions.IEmailSender, TestEmailSender>();

                // Add test auth
                services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
                services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(opts => opts.DefaultScheme = "Test");

                // Ensure DB is created
                var sp = services.BuildServiceProvider();
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();

                // seed a test user for password-reset flow
                try
                {
                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var user = new ApplicationUser { UserName = "testuser", Email = "testuser@example.com", EmailConfirmed = true };
                    var createRes = userManager.CreateAsync(user, "Test@12345").GetAwaiter().GetResult();
                }
                catch { }

                // seed a product
                db.Products.Add(new Product { Id = Guid.NewGuid(), Name = "Integration Lipstick", Price = 1500m, Inventory = 10, Available = true, BrandName = "IntB", Category = "Makeup" });
                db.SaveChanges();
            });
        }
    }

    // Use shared test fakes from TestFakes.cs

    public class OrdersIntegrationTests : IClassFixture<CustomFactory>
    {
        private readonly CustomFactory _factory;

        public OrdersIntegrationTests(CustomFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CheckoutFromCart_Integration_Works()
        {
            var client = _factory.CreateClient();

            // read a product id from the seeded DB
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.Products.FirstAsync();

            var cart = new { Cart = new { Items = new[] { new { ProductId = product.Id, Quantity = 2 } } }, FullName = "Test User", Line1 = "Addr", Line2 = (string?)null, City = "Lagos", State = "LA", Country = "NG", PostalCode = "100001", PhoneNumber = "0800", ExpectedDeliveryDate = (DateTime?)null, EmailForPayment = "test@example.com", CallbackUrl = "https://example/callback" };

            var resp = await client.PostAsJsonAsync("/api/orders/checkout", cart);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var authUrl = doc.RootElement.GetProperty("authorizationUrl").GetString();
            Assert.Equal("https://pay.test/authorize", authUrl);

            var pAfter = await db.Products.FirstAsync(p => p.Id == product.Id);
            Assert.Equal(8, pAfter.Inventory);
        }
    }
}
