using System;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using Easshas.Application.Abstractions;
using Easshas.Domain.ValueObjects;
using Easshas.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Easshas.Infrastructure.Services
{
    public class ConversationService : IConversationService
    {
        private readonly ICommerceAgent _agent;
        private readonly IChatSessionService _sessions;
        private readonly IProductService _products;
        private readonly IOrderService _orders;
        private readonly IPaystackService _paystack;
        private readonly UserManager<ApplicationUser> _users;
        private readonly IConfiguration _config;

        public ConversationService(ICommerceAgent agent, IChatSessionService sessions, IProductService products, IOrderService orders, IPaystackService paystack, UserManager<ApplicationUser> users, IConfiguration config)
        {
            _agent = agent;
            _sessions = sessions;
            _products = products;
            _orders = orders;
            _paystack = paystack;
            _users = users;
            _config = config;
        }

        public async Task<string> HandleMessageAsync(string fromPhone, string text)
        {
            var session = await _sessions.GetOrCreateAsync(fromPhone);
            var intent = await _agent.ProcessQueryAsync(text, session.State);

            // Purchase Intent
            if (intent.Intent == "purchase")
            {
                var prodName = intent.ProductName ?? "the product";
                var product = await _products.GetByNameAsync(prodName);
                if (product == null) return await _agent.GenerateResponseAsync(intent, $"However, I couldn't find a product named '{prodName}' in our catalog.");

                session.ProductId = product.Id;
                session.Quantity = intent.Quantity > 0 ? intent.Quantity : 1;
                session.State = "AwaitingEmail";

                if (!string.IsNullOrWhiteSpace(intent.Email))
                {
                    session.EmailForPayment = intent.Email;
                    session.State = "AwaitingAddress";
                }

                await _sessions.SaveAsync(session);

                if (session.State == "AwaitingEmail")
                    return await _agent.GenerateResponseAsync(intent, $"Great choice! To proceed, I'll need your email address to send the receipt.");

                return await _agent.GenerateResponseAsync(intent, $"Got it. Now, please provide your delivery address.");
            }

            // Email Intent
            if (intent.Intent == "email" || (session.State == "AwaitingEmail" && !string.IsNullOrWhiteSpace(intent.Email)))
            {
                session.EmailForPayment = intent.Email ?? intent.RawQuery; // Fallback if direct scan fails
                session.State = "AwaitingAddress";
                await _sessions.SaveAsync(session);
                return "Thanks! Now, where should we deliver your order?";
            }

            // Address Intent
            if (intent.Intent == "address" || (session.State == "AwaitingAddress" && !string.IsNullOrWhiteSpace(text)))
            {
                // Basic parsing of address if not already done by agent
                session.Line1 = intent.Address ?? text;
                session.City = "Lagos"; // Default or extracted
                session.StateRegion = "LA";
                session.Country = "NG";
                session.PostalCode = "100001";
                await _sessions.SaveAsync(session);

                var user = await _users.FindByNameAsync(fromPhone) ?? await _users.FindByEmailAsync(session.EmailForPayment ?? string.Empty);
                if (user == null)
                {
                    user = new ApplicationUser { UserName = fromPhone, PhoneNumber = fromPhone, Email = session.EmailForPayment };
                    var pwd = "P@ss" + Guid.NewGuid().ToString("N").Substring(0, 12);
                    await _users.CreateAsync(user, pwd);
                }
                session.UserId = Guid.Parse(user.Id.ToString());

                var addr = new Address(user.FullName ?? fromPhone, session.Line1!, null, session.City!, session.StateRegion!, session.Country!, session.PostalCode!, user.PhoneNumber ?? fromPhone);
                var order = await _orders.CreateOrderAsync(session.UserId.Value, session.ProductId!.Value, session.Quantity ?? 1, addr, null);

                var reference = $"ORD-{order.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                var callbackUrl = _config["Payment:CallbackUrl"] ?? "https://yourapp/callback";
                var init = await _paystack.InitializeTransactionAsync(order.TotalAmount, session.EmailForPayment!, reference, callbackUrl);
                await _orders.SetPaystackReferenceAsync(order.Id, init.Reference);

                session.OrderId = order.Id;
                session.PaystackReference = init.Reference;
                session.State = "AwaitingPayment";
                await _sessions.SaveAsync(session);

                return $"Perfect! You can complete your payment here: {init.AuthorizationUrl}. Your order reference is {init.Reference}.";
            }

            // Status Intent
            if (intent.Intent == "track_order")
            {
                if (string.IsNullOrWhiteSpace(session.PaystackReference))
                    return "I don't see any active orders for you right now. Would you like to buy something?";

                var order = await _orders.GetOrderByReferenceAsync(session.PaystackReference!);
                if (order == null) return "I couldn't find your order details. Please double-check your reference.";

                return await _agent.GenerateResponseAsync(intent, $"Your order #{order.Id} is currently {order.Status}. Total: {order.TotalAmount} {order.Currency}.");
            }

            // Simple keyword-based product lookup fallback (e.g., "which lipstick do you have")
            var query = text?.ToLowerInvariant() ?? string.Empty;
            if (query.Contains("lipstick") || query.Contains("lip stick") || query.Contains("lipcolour") || query.Contains("lip colour") || query.Contains("lip gloss"))
            {
                var all = await _products.ListAsync(0, 200);
                var matches = all.Where(p => (!string.IsNullOrWhiteSpace(p.Name) && p.Name.ToLowerInvariant().Contains("lipstick"))
                                            || (!string.IsNullOrWhiteSpace(p.Description) && p.Description.ToLowerInvariant().Contains("lipstick"))
                                            || (!string.IsNullOrWhiteSpace(p.Category) && p.Category.ToLowerInvariant().Contains("lipstick"))
                                            || (!string.IsNullOrWhiteSpace(p.BrandName) && p.BrandName.ToLowerInvariant().Contains("lipstick")))
                                      .ToList();

                if (matches.Any())
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"I found {matches.Count} lipstick(s) in our catalog:");
                    foreach (var p in matches.Take(10))
                    {
                        sb.AppendLine($"- {p.Name} — {p.BrandName} — ₦{p.Price:0.00} — SKU: {p.Sku}");
                    }
                    if (matches.Count > 10) sb.AppendLine($"And {matches.Count - 10} more. Reply with the product name to get details or say 'add to cart' to purchase.");
                    return await _agent.GenerateResponseAsync(intent, sb.ToString());
                }

                return await _agent.GenerateResponseAsync(intent, "I couldn't find any lipsticks right now. Would you like me to show similar products or be notified when they're available?");
            }

            return "I'm your Easshas Shopping Assistant. You can ask me to buy products, track your orders, or ask about prices!";
        }
    }
}
