using System;
using System.Threading.Tasks;
using Easshas.Domain.Entities;
using Easshas.Domain.ValueObjects;
using System.Collections.Generic;

namespace Easshas.Application.Abstractions
{
    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(Guid userId, Guid productId, int quantity, Address billingAddress, DateTime? expectedDeliveryDate);
        Task<Order> CreateOrderFromCartAsync(Guid userId, CartDto cart, Address billingAddress, DateTime? expectedDeliveryDate);
        Task<bool> SetPaystackReferenceAsync(Guid orderId, string reference);
        Task<bool> MarkOrderPaidAsync(string reference);
        Task<Order?> GetOrderByReferenceAsync(string reference);
        Task<Order?> GetOrderForUserAsync(Guid userId, Guid orderId);
        Task<List<Order>> ListOrdersForUserAsync(Guid userId, int skip = 0, int take = 50);
        Task<Payment?> GetLatestPaymentForOrderAsync(Guid orderId);

        Task<int> GetProductPurchaseCountAsync(Guid productId);

        // Get order by tracking number
        Task<Order?> GetByTrackingNumberAsync(string trackingNumber);

        // List orders still pending payment, older than minAgeMinutes
        Task<List<Order>> ListPendingOrdersAsync(int minAgeMinutes);
        Task<int> ReleaseExpiredPendingOrdersAsync(int olderThanMinutes);
    }
}
