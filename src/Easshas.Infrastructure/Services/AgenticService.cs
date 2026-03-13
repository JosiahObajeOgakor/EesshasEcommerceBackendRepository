using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Easshas.Application.Abstractions;
using Easshas.Application.Models;
using Easshas.Domain.Entities;
using Easshas.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Easshas.Infrastructure.Services
{
    public class AgenticService : IAgenticService
    {
        private readonly AppDbContext _db;
        private readonly IProductService _products;
        private readonly IOrderService _orders;
        private readonly IPaystackService _paystack;
        private readonly IPlanner _planner;
        private readonly IEmbeddingService _embedder;
        private readonly IVectorStore _vectors;
        private readonly ILogger<AgenticService> _logger;

        public AgenticService(AppDbContext db, IProductService products, IOrderService orders, IPaystackService paystack, IPlanner planner, IEmbeddingService embedder, IVectorStore vectors, ILogger<AgenticService> logger)
        {
            _db = db;
            _products = products;
            _orders = orders;
            _paystack = paystack;
            _planner = planner;
            _embedder = embedder;
            _vectors = vectors;
            _logger = logger;
        }

        public async Task<AgenticResponse> HandleMessageAsync(string userId, string text, string? sessionId = null, string? language = "en")
        {
            var conv = new AgentConversation { SessionId = sessionId ?? Guid.NewGuid().ToString(), UserId = ParseGuid(userId) };
            _db.AgentConversations.Add(conv);
            await _db.SaveChangesAsync();

            var msg = new AgentMessage { ConversationId = conv.Id, Role = "user", Text = text, Language = language };
            _db.AgentMessages.Add(msg);
            await _db.SaveChangesAsync();

            // RAG: get embedding and retrieve user-specific facts
            var userGuid = ParseGuid(userId);
            var embedding = await _embedder.GetEmbeddingAsync(text ?? string.Empty);
            List<string> facts = new();
            if (embedding != null && embedding.Length > 0)
            {
                facts = await _vectors.SearchAsync(userGuid, embedding, 5);
            }

            // Assemble context for planner
            var contextBuilder = new System.Text.StringBuilder();
            if (facts != null && facts.Count > 0)
            {
                contextBuilder.AppendLine("User facts:");
                foreach (var f in facts) contextBuilder.AppendLine("- " + f);
                contextBuilder.AppendLine("---");
            }
            contextBuilder.AppendLine("User query:");
            contextBuilder.AppendLine(text ?? string.Empty);

            var augmented = contextBuilder.ToString();

            // Use planner (LLM) to produce structured plan
            var plan = await _planner.PlanAsync(userId, augmented, language);
            if (plan == null || plan.Count == 0)
            {
                return new AgenticResponse { State = AgentState.Idle, Reply = "I can help with purchases and tracking. Ask me to buy a product." };
            }

            var response = new AgenticResponse { State = AgentState.Planning, Reply = "Planned actions", Plan = plan };

            // Persist plan as action logs
            foreach (var a in plan)
            {
                var al = new AgentActionLog { ConversationId = conv.Id, ActionId = a.Id, ActionType = a.Type, Status = "Pending", Attempts = 0 };
                _db.AgentActionLogs.Add(al);
            }
            await _db.SaveChangesAsync();

            // Execute sequentially
            response.State = AgentState.Executing;
            var execLog = new List<object>();
            foreach (var a in plan)
            {
                var log = await ExecuteActionAsync(conv, a);
                execLog.Add(log);
            }

            response.ExecutionLog = execLog;
            // Simple evaluation: if any failed -> Failed otherwise Completed
            var failed = _db.AgentActionLogs.Where(l => l.ConversationId == conv.Id && l.Status == "Failed").Any();
            response.State = failed ? AgentState.Failed : AgentState.Completed;
            response.Reply = failed ? "I couldn't complete some steps; please try again or modify your request." : "All done — I started the purchase flow. Check your email for payment link.";

            return response;
        }

        private Guid? ParseGuid(string s)
        {
            if (Guid.TryParse(s, out var g)) return g; return null;
        }

        private async Task<object> ExecuteActionAsync(AgentConversation conv, AgentAction action)
        {
            try
            {
                if (action.Type == "CheckInventory")
                {
                    var query = action.Params.TryGetValue("query", out var q) ? q?.ToString() ?? string.Empty : string.Empty;
                    var p = await _products.GetByNameAsync(query ?? string.Empty);
                    var success = p != null;
                    await UpdateActionLog(conv.Id, action, success ? "Success" : "Failed", new { found = success, productId = p?.Id });
                    return new { action = action.Type, success, product = p?.Name };
                }
                if (action.Type == "CreateOrder")
                {
                    var query = action.Params.TryGetValue("query", out var q) ? q?.ToString() ?? string.Empty : string.Empty;
                    var p = await _products.GetByNameAsync(query ?? string.Empty);
                    if (p == null)
                    {
                        await UpdateActionLog(conv.Id, action, "Failed", new { reason = "Product not found" });
                        return new { action = action.Type, success = false };
                    }
                    // create a minimal order for the user if UserId present
                    if (conv.UserId == null)
                    {
                        await UpdateActionLog(conv.Id, action, "Failed", new { reason = "User unknown" });
                        return new { action = action.Type, success = false };
                    }
                    var addr = new Easshas.Domain.ValueObjects.Address("", "", null, "", "", "", "", "");
                    var order = await _orders.CreateOrderAsync(conv.UserId.Value, p.Id, 1, addr, null);
                    await UpdateActionLog(conv.Id, action, "Success", new { orderId = order.Id });
                    return new { action = action.Type, success = true, orderId = order.Id };
                }
                if (action.Type == "InitPayment")
                {
                    // find latest order for conversation
                    var act = _db.AgentActionLogs.Where(a => a.ConversationId == conv.Id && a.ActionType == "CreateOrder").OrderByDescending(a => a.CreatedAt).FirstOrDefault();
                    if (act == null) { await UpdateActionLog(conv.Id, action, "Failed", new { reason = "No order" }); return new { action = action.Type, success = false }; }
                    var payload = act.ResultJson;
                    // We stored resultJson as plain text; try to parse by searching for orderId int guid inside
                    // Fallback: pick latest Order by createdAt for this user
                    var order = _db.Orders.OrderByDescending(o => o.CreatedAt).FirstOrDefault(o => o.UserId == conv.UserId);
                    if (order == null) { await UpdateActionLog(conv.Id, action, "Failed", new { reason = "No order found for user" }); return new { action = action.Type, success = false }; }
                    var reference = $"ORD-{order.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
                    var callbackUrl = action.Params.TryGetValue("callbackUrl", out var cb) ? cb?.ToString() ?? string.Empty : "";
                    var init = await _paystack.InitializeTransactionAsync(order.TotalAmount, string.Empty, reference, callbackUrl);
                    await _orders.SetPaystackReferenceAsync(order.Id, init.Reference);
                    await UpdateActionLog(conv.Id, action, "Success", new { authorizationUrl = init.AuthorizationUrl, reference = init.Reference });
                    return new { action = action.Type, success = true, authorizationUrl = init.AuthorizationUrl, reference = init.Reference };
                }

                await UpdateActionLog(conv.Id, action, "Failed", new { reason = "Unknown action" });
                return new { action = action.Type, success = false };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Action execution failed");
                await UpdateActionLog(conv.Id, action, "Failed", new { exception = ex.Message });
                return new { action = action.Type, success = false, error = ex.Message };
            }
        }

        private async Task UpdateActionLog(Guid convId, AgentAction a, string status, object result)
        {
            var log = _db.AgentActionLogs.FirstOrDefault(l => l.ConversationId == convId && l.ActionId == a.Id);
            if (log == null) return;
            log.Status = status;
            log.ResultJson = System.Text.Json.JsonSerializer.Serialize(result);
            log.Attempts += 1;
            await _db.SaveChangesAsync();
        }
    }
}
