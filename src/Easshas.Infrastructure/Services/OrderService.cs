

using System;
using System.Linq;
using System.Threading.Tasks;
using Easshas.Application.Abstractions;
using Easshas.Domain.Entities;
using Easshas.Domain.Enums;
using Easshas.Domain.ValueObjects;
using Easshas.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Easshas.Infrastructure.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notifications;

        public OrderService(AppDbContext db, INotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public async Task<Order> CreateOrderAsync(Guid userId, Guid productId, int quantity, Address billingAddress, DateTime? expectedDeliveryDate)
        {
            // Reserve inventory at order creation time
            using var transaction = await _db.Database.BeginTransactionAsync();
            // Lock the product row to avoid concurrent reservations causing oversell
            var product = await _db.Products.FromSqlRaw("SELECT * FROM \"Products\" WHERE \"Id\" = {0} FOR UPDATE", productId).FirstOrDefaultAsync();
            if (product == null) throw new InvalidOperationException("Product not found");
            if (!product.Available) throw new InvalidOperationException("Product is not available");
            if (quantity <= 0) throw new InvalidOperationException("Quantity must be greater than zero");
            if (product.Inventory < quantity) throw new InvalidOperationException("Insufficient inventory for requested quantity");

            var order = new Order
            {
                UserId = userId,
                BillingAddress = billingAddress,
                ExpectedDeliveryDate = expectedDeliveryDate,
                Status = OrderStatus.Pending,
                TrackingNumber = $"eeshaslipsgloss2026#{Guid.NewGuid().ToString("N").Substring(0, 12)}",
                StatusHistory = new List<OrderStatusHistory>
                {
                    new OrderStatusHistory { Status = OrderStatus.Pending, ChangedAt = DateTime.UtcNow, Note = "Order created" }
                }
            };

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                NameSnapshot = product.Name,
                UnitPrice = product.Price,
                Quantity = quantity
            });

            // Reserve inventory and update availability based on remaining stock
            product.Inventory -= quantity;
            product.Available = product.Inventory > 0;

            order.TotalAmount = order.Items.Sum(i => i.Total);
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            // Notify admin of new order
            await _notifications.NotifyAdminNewOrderAsync(order);

            // Notify low stock if applicable
            if (product.Inventory <= 5)
            {
                await _notifications.NotifyLowStockAsync(product);
            }

            return order;
        }

        public async Task<Order> CreateOrderFromCartAsync(Guid userId, Application.Abstractions.CartDto cart, Address billingAddress, DateTime? expectedDeliveryDate)
        {
            if (cart == null || cart.Items == null || !cart.Items.Any())
                throw new InvalidOperationException("Cart is empty");
            // Reserve inventory for all items atomically
            using var transaction = await _db.Database.BeginTransactionAsync();
            var order = new Order
            {
                UserId = userId,
                BillingAddress = billingAddress,
                ExpectedDeliveryDate = expectedDeliveryDate,
                Status = OrderStatus.Pending,
                TrackingNumber = $"eeshascart2026#{Guid.NewGuid().ToString("N").Substring(0, 12)}",
                StatusHistory = new List<OrderStatusHistory>
                {
                    new OrderStatusHistory { Status = OrderStatus.Pending, ChangedAt = DateTime.UtcNow, Note = "Order created from cart" }
                }
            };

            foreach (var item in cart.Items)
            {
                // Lock each product row to avoid concurrent reservations causing oversell
                var product = await _db.Products.FromSqlRaw("SELECT * FROM \"Products\" WHERE \"Id\" = {0} FOR UPDATE", item.ProductId).FirstOrDefaultAsync();
                if (product == null) throw new InvalidOperationException($"Product not found: {item.ProductId}");
                if (!product.Available) throw new InvalidOperationException($"Product is not available: {product.Name}");
                if (item.Quantity <= 0) throw new InvalidOperationException("Quantity must be greater than zero");
                if (product.Inventory < item.Quantity) throw new InvalidOperationException($"Insufficient inventory for requested quantity for product {product.Name}");

                // Reserve and update availability
                product.Inventory -= item.Quantity;
                product.Available = product.Inventory > 0;

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    NameSnapshot = product.Name,
                    UnitPrice = product.Price,
                    Quantity = item.Quantity
                });
            }

            order.TotalAmount = order.Items.Sum(i => i.Total);
            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            // Notify admin and low-stock alerts
            await _notifications.NotifyAdminNewOrderAsync(order);
            var lowStockProducts = await _db.Products.Where(p => p.Inventory <= 5).ToListAsync();
            foreach (var lp in lowStockProducts)
            {
                await _notifications.NotifyLowStockAsync(lp);
            }

            return order;
        }

        public async Task<bool> SetPaystackReferenceAsync(Guid orderId, string reference)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return false;
            order.PaystackReference = reference;
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarkOrderPaidAsync(string reference)
        {
            // Use a transaction for atomicity
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var order = await _db.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.PaystackReference == reference);

                if (order == null) return false;
                if (order.Status == OrderStatus.Paid) return true; // Already paid

                order.Status = OrderStatus.Paid;
                order.StatusHistory.Add(new OrderStatusHistory { Status = OrderStatus.Paid, ChangedAt = DateTime.UtcNow, Note = "Payment verified" });

                foreach (var item in order.Items)
                {
                    // Inventory reservation occurs at order creation; no deduction here.
                }

                var existingPayment = await _db.Payments.FirstOrDefaultAsync(p => p.Reference == reference);
                if (existingPayment == null)
                {
                    _db.Payments.Add(new Payment
                    {
                        UserId = order.UserId != Guid.Empty ? order.UserId : null,
                        OrderId = order.Id,
                        Amount = order.TotalAmount,
                        Currency = order.Currency,
                        Reference = reference,
                        Status = "Success",
                        PaidAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                // Trigger notifications
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == order.UserId);
                var userName = user?.FullName ?? user?.UserName ?? string.Empty;
                await _notifications.NotifyOrderPaidAsync(order, user?.Email ?? string.Empty, user?.PhoneNumber ?? string.Empty, userName);

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public Task<Order?> GetOrderByReferenceAsync(string reference)
        {
            return _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.PaystackReference == reference);
        }

        public Task<Order?> GetOrderForUserAsync(Guid userId, Guid orderId)
        {
            return _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId);
        }

        public async Task<List<Order>> ListOrdersForUserAsync(Guid userId, int skip = 0, int take = 50)
        {
            return await _db.Orders
                .Include(o => o.Items)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
        }

        public Task<Payment?> GetLatestPaymentForOrderAsync(Guid orderId)
        {
            return _db.Payments
                .Where(p => p.OrderId == orderId)
                .OrderByDescending(p => p.PaidAt)
                .FirstOrDefaultAsync();
        }

        public async Task<int> GetProductPurchaseCountAsync(Guid productId)
        {
            return await _db.Orders
                .Where(o => o.Status == OrderStatus.Paid)
                .SelectMany(o => o.Items)
                .Where(i => i.ProductId == productId)
                .SumAsync(i => i.Quantity);
        }

        public async Task<List<Order>> ListPendingOrdersAsync(int minAgeMinutes)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-Math.Abs(minAgeMinutes));
            return await _db.Orders
                .Include(o => o.Items)
                .Where(o => o.Status == OrderStatus.Pending
                            && o.CreatedAt <= cutoff
                            && !string.IsNullOrWhiteSpace(o.PaystackReference))
                .OrderBy(o => o.CreatedAt)
                .Take(200)
                .ToListAsync();
        }
        public async Task<Order?> GetByTrackingNumberAsync(string trackingNumber)
        {
            return await _db.Orders
                .Include(o => o.Items)
                .Include(o => o.StatusHistory)
                .FirstOrDefaultAsync(o => o.TrackingNumber == trackingNumber);
        }

        public async Task<int> ReleaseExpiredPendingOrdersAsync(int olderThanMinutes)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-Math.Abs(olderThanMinutes));
            var toRelease = await _db.Orders
                .Include(o => o.Items)
                .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt <= cutoff)
                .ToListAsync();

            if (!toRelease.Any()) return 0;

            foreach (var order in toRelease)
            {
                // Restore inventory for each item
                foreach (var item in order.Items)
                {
                    var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    if (product != null)
                    {
                        product.Inventory += item.Quantity;
                        if (product.Inventory > 0) product.Available = true;
                    }
                }

                order.Status = OrderStatus.Cancelled;
                order.StatusHistory.Add(new OrderStatusHistory { Status = OrderStatus.Cancelled, ChangedAt = DateTime.UtcNow, Note = "Automatically cancelled due to payment timeout" });
            }

            await _db.SaveChangesAsync();

            // Notify admin about cancellations
            foreach (var order in toRelease)
            {
                await _notifications.NotifyOrderStatusChangedAsync(order, string.Empty, string.Empty, order.Status.ToString(), "Cancelled due to timeout");
            }

            return toRelease.Count;
        }
    }
}
