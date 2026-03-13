using System;
using System.Threading.Tasks;
using Easshas.Domain.Entities;

namespace Easshas.Application.Abstractions
{
    public interface INotificationService
    {
        Task NotifyOrderPaidAsync(Order order, string userEmail, string userPhone, string? userName = null);
            Task NotifyOrderCreatedAsync(Order order, string userEmail, string userPhone, string trackingUrl, string? userName = null);
        Task NotifyOrderStatusChangedAsync(Order order, string userEmail, string userPhone, string status, string? note = null, string? userName = null);
        Task NotifyLowStockAsync(Product product);
        Task NotifyAdminNewOrderAsync(Order order);
        Task BroadcastProductUpdateAsync(Product product);
    }
}
